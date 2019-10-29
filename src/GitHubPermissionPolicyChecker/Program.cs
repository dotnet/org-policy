using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Options;

using Octokit;

using Terrajobst.Csv;
using Terrajobst.GitHubCaching;
using Terrajobst.Ospo;

namespace GitHubPermissionPolicyChecker
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            string orgName = null;
            string outputFileName = null;
            string cacheLocation = null;
            string githubToken = null;
            string ospoToken = null;
            string policyRepo = null;
            var help = false;

            var options = new OptionSet()
                .Add("orgName", "The {name} of the GitHub organization", v => orgName = v)
                .Add("o|output=", "The {path} where the output .csv file should be written to.", v => outputFileName = v)
                .Add("cache-location=", "The {path} where the .json cache should be written to.", v => cacheLocation = v)
                .Add("github-token=", "The GitHub API {token} to be used.", v => githubToken = v)
                .Add("ospo-token=", "The OSPO API {token} to be used.", v => ospoToken = v)
                .Add("policy-repo=", "The GitHub {repo} policy violations should be file in.", v => policyRepo = v)
                .Add("h|?|help", null, v => help = true, true)
                .Add(new ResponseFileSource());

            try
            {
                var unprocessed = options.Parse(args);

                if (help)
                {
                    var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                    Console.Error.WriteLine($"usage: {exeName} <org-name>");
                    options.WriteOptionDescriptions(Console.Error);
                    return;
                }

                if (unprocessed.Count > 0)
                {
                    orgName = unprocessed[0];
                    unprocessed.RemoveAt(0);
                }

                if (orgName == null)
                {
                    Console.Error.WriteLine($"error: <org-name> must be specified");
                    return;
                }

                if (unprocessed.Any())
                {
                    foreach (var option in unprocessed)
                        Console.Error.WriteLine($"error: unrecognized argument {option}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return;
            }

            if (outputFileName == null && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: you must specify an output path because you don't have Excel.");
                return;
            }

            await RunAsync(orgName, outputFileName, cacheLocation, githubToken, ospoToken, policyRepo);
        }

        private static async Task RunAsync(string orgName, string outputFileName, string cacheLocation, string githubToken, string ospoToken, string policyRepo)
        {
            var isForExcel = outputFileName == null;
            var gitHubClient = await GitHubClientFactory.CreateAsync(githubToken);
            var ospoClient = await OspoClientFactory.CreateAsync(ospoToken);
            var loader = new CachedOrgLoader(gitHubClient, Console.Out, cacheLocation, forceUpdate: false);
            var cachedOrg = await loader.LoadAsync(orgName);
            var userLinks = await MicrosoftUserLinks.LoadAsync(ospoClient);
            var context = new PolicyAnalysisContext(cachedOrg, userLinks);
            var violations = GetRules().SelectMany(r => r.GetViolations(context)).ToArray();

            SaveVioloations(orgName, outputFileName, isForExcel, violations);

            if (!string.IsNullOrEmpty(policyRepo))
                await FilePolicyViolationsAsync(gitHubClient, orgName, policyRepo, violations);
        }

        private static void SaveVioloations(string orgName, string outputFileName, bool isForExcel, IReadOnlyList<PolicyViolation> violations)
        {
            var csvDocument = new CsvDocument("org", "rule", "fingerprint", "violation", "repo", "user", "team", "receivers");
            using (var writer = csvDocument.Append())
            {
                foreach (var violation in violations)
                {
                    writer.Write(orgName);
                    writer.Write(violation.DiagnosticId);
                    writer.Write(violation.Fingerprint.ToString());
                    writer.Write(violation.Title);

                    if (violation.Repo == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Repo.Url, violation.Repo.Name, isForExcel);

                    if (violation.User == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.User.Url, violation.User.Login, isForExcel);

                    if (violation.Team == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Team.Url, violation.Team.Name, isForExcel);

                    var receivers = string.Join(", ", violation.Assignees.Select(r => r.Login));
                    writer.Write(receivers);

                    writer.WriteLine();
                }
            }

            if (outputFileName == null)
            {
                csvDocument.ViewInExcel();
            }
            else
            {
                csvDocument.Save(outputFileName);
            }
        }

        private static async Task FilePolicyViolationsAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            if (policyRepo.Contains("/"))
            {
                var parts = policyRepo.Split('/');
                orgName = parts[0];
                policyRepo = parts[1];
            }

            await CreateLabelsAsync(client, orgName, policyRepo, violations);
            await CreateIssuesAsync(client, orgName, policyRepo, violations);
        }

        private static async Task CreateLabelsAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            await client.PrintProgressAsync(Console.Out, "Loading label list");
            var existingLabels = await client.Issue.Labels.GetAllForRepository(orgName, policyRepo);

            var existingLabelNames = existingLabels.ToDictionary(l => l.Name);
            var desiredLabelNames = violations.Select(v => v.DiagnosticId).Distinct().Concat(new[] { "violation" }).ToArray();
            var missingLabelNames = desiredLabelNames.Where(di => !existingLabelNames.ContainsKey(di)).ToList();

            var i = 0;

            foreach (var missingLabelName in missingLabelNames)
            {
                await client.PrintProgressAsync(Console.Out, "Create label", missingLabelName, i++, missingLabelNames.Count);

                var newLabel = new NewLabel(missingLabelName, "cfd3d7");
                await client.Issue.Labels.Create(orgName, policyRepo, newLabel);
            }
        }

        private static async Task CreateIssuesAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            await client.PrintProgressAsync(Console.Out, "Loading issue list");
            var issueRequest = new RepositoryIssueRequest();
            issueRequest.State = ItemStateFilter.All;
            issueRequest.Labels.Add("violation");
            var existingIssues = await client.Issue.GetAllForRepository(orgName, policyRepo, issueRequest);

            var newViolations = violations.Where(v => !existingIssues.Any(e => e.Title.Contains(v.Fingerprint.ToString()))).ToList();
            var i = 0;

            foreach (var newViolation in newViolations)
            {
                await client.PrintProgressAsync(Console.Out, "Filing issue", newViolation.Title, i++, newViolations.Count);

                var title = $"{newViolation.Title} ({newViolation.Fingerprint})";
                var body = $"{newViolation.Body}";

                body += Environment.NewLine + Environment.NewLine;
                body += "### Assignees" + Environment.NewLine;
                foreach (var assignee in newViolation.Assignees)
                    body += $"* {assignee.Markdown()}" + Environment.NewLine;

                var newIssue = new NewIssue(title)
                {
                    Body = body,
                    Labels =
                    {
                        newViolation.DiagnosticId,
                        "violation"
                    }
                };

                //foreach (var assignee in violation.Assignees)
                //    newIssue.Assignees.Add(assignee.Login);

                await client.Issue.Create(orgName, policyRepo, newIssue);
            }
        }

        private static IReadOnlyList<PolicyRule> GetRules()
        {
            return typeof(Program).Assembly.GetTypes()
                                           .Where(t => !t.IsAbstract &&
                                                       t.GetConstructor(Array.Empty<Type>()) != null &&
                                                       typeof(PolicyRule).IsAssignableFrom(t))
                                           .Select(t => Activator.CreateInstance(t))
                                           .Cast<PolicyRule>()
                                           .ToList();
        }
    }
}
