using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;
using Microsoft.DotnetOrg.Policies;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop
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
                .Add("org=", "The {name} of the GitHub organization", v => orgName = v)
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
                    Console.Error.WriteLine($"usage: {exeName} --org <org> [OPTIONS]+");
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
                    Console.Error.WriteLine($"error: --org must be specified");
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
            var violations = PolicyRunner.Run(context);

            SaveVioloations(orgName, outputFileName, isForExcel, violations);

            if (!string.IsNullOrEmpty(policyRepo))
                await FilePolicyViolationsAsync(gitHubClient, orgName, policyRepo, violations);
        }

        private static readonly string AreaViolationLabel = "area-violation";
        private static readonly string PolicyOverrideLabel = "policy-override";

        private static void SaveVioloations(string orgName, string outputFileName, bool isForExcel, IReadOnlyList<PolicyViolation> violations)
        {
            var csvDocument = new CsvDocument("org", "rule", "fingerprint", "violation", "repo", "user", "team", "receivers");
            using (var writer = csvDocument.Append())
            {
                foreach (var violation in violations)
                {
                    writer.Write(orgName);
                    writer.Write(violation.Descriptor.DiagnosticId);
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

            var existingIssues = await GetIssuesAsync(client, orgName, policyRepo);
            await CreateIssuesAsync(client, orgName, policyRepo, violations, existingIssues);
            await ReopenIssuesAsync(client, orgName, policyRepo, violations, existingIssues);
            await CloseIssuesAsync(client, orgName, policyRepo, violations, existingIssues);
        }

        private static async Task CreateLabelsAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            await client.PrintProgressAsync(Console.Out, "Loading label list");
            var existingLabels = await client.Issue.Labels.GetAllForRepository(orgName, policyRepo);

            var existingLabelNames = existingLabels.ToDictionary(l => l.Name);
            var desiredLabelNames = violations.Select(v => v.Descriptor.DiagnosticId).Distinct().Concat(new[] { AreaViolationLabel, PolicyOverrideLabel }).ToArray();
            var missingLabelNames = desiredLabelNames.Where(di => !existingLabelNames.ContainsKey(di)).ToList();

            var descriptors = violations.Select(v => v.Descriptor).Distinct().ToDictionary(d => d.DiagnosticId);

            var i = 0;

            foreach (var missingLabelName in missingLabelNames)
            {
                await client.PrintProgressAsync(Console.Out, "Create label", missingLabelName, i++, missingLabelNames.Count);

                string color;
                string description;

                if (missingLabelName == AreaViolationLabel)
                {
                    color = "d4c5f9";
                    description = "Issues representing policy violations";
                }
                else if (missingLabelName == PolicyOverrideLabel)
                {
                    color = "3e820b";
                    description = "Marks an issue as a deliberate policy violation";
                }
                else
                {
                    var descriptor = descriptors[missingLabelName];
                    color = descriptor.Severity == PolicySeverity.Error
                        ? "e11d21"
                        : "fbca04";
                    description = descriptor.Title;
                }

                var newLabel = new NewLabel(missingLabelName, color)
                {
                    Description = description
                };
                await client.Issue.Labels.Create(orgName, policyRepo, newLabel);
            }
        }

        private static async Task<IReadOnlyList<Issue>> GetIssuesAsync(GitHubClient client, string orgName, string policyRepo)
        {
            await client.PrintProgressAsync(Console.Out, "Loading issue list");
            var issueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.All
            };
            issueRequest.Labels.Add(AreaViolationLabel);
            var existingIssues = await client.Issue.GetAllForRepository(orgName, policyRepo, issueRequest);
            return existingIssues;
        }

        private static async Task CreateIssuesAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations, IReadOnlyList<Issue> existingIssues)
        {
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
                        newViolation.Descriptor.DiagnosticId,
                        AreaViolationLabel
                    }
                };

                //foreach (var assignee in violation.Assignees)
                //    newIssue.Assignees.Add(assignee.Login);

                await client.Issue.Create(orgName, policyRepo, newIssue);
            }
        }

        private static async Task ReopenIssuesAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations, IReadOnlyList<Issue> existingIssues)
        {
            var fingerprints = new HashSet<Guid>(violations.Select(v => v.Fingerprint));

            var oldIssues = existingIssues.Where(issue => issue.State.Value == ItemState.Closed &&
                                                              !issue.Labels.Any(l => l.Name == PolicyOverrideLabel))
                                           .Select(issue => (Fingerprint: GetFingerprint(issue.Title), Issue: issue))
                                           .Where(t => t.Fingerprint != null && fingerprints.Contains(t.Fingerprint.Value))
                                           .Select(t => t.Issue)                                              
                                           .ToList();

            var i = 0;

            foreach (var oldIssue in oldIssues)
            {
                await client.PrintProgressAsync(Console.Out, "Reopening issue", oldIssue.Title, i++, oldIssues.Count);

                await client.Issue.Comment.Create(orgName, policyRepo, oldIssue.Number, "The violation still exists.");

                var issueUpdate = new IssueUpdate
                {
                    State = ItemState.Open
                };
                await client.Issue.Update(orgName, policyRepo, oldIssue.Number, issueUpdate);
            }
        }

        private static async Task CloseIssuesAsync(GitHubClient client, string orgName, string policyRepo, IReadOnlyList<PolicyViolation> violations, IReadOnlyList<Issue> existingIssues)
        {
            var newFingerprints = new HashSet<Guid>(violations.Select(v => v.Fingerprint));

            var solvedIssues = existingIssues.Select(issue => (Fingerprint: GetFingerprint(issue.Title), Issue: issue))
                                              .Where(t => t.Fingerprint != null && !newFingerprints.Contains(t.Fingerprint.Value))
                                              .Select(t => t.Issue)
                                              .ToList();

            var i = 0;

            foreach (var solvedIssue in solvedIssues)
            {
                await client.PrintProgressAsync(Console.Out, "Closing issue", solvedIssue.Title, i++, solvedIssues.Count);

                await client.Issue.Comment.Create(orgName, policyRepo, solvedIssue.Number, "The violation was addressed.");

                var issueUpdate = new IssueUpdate
                {
                    State = ItemState.Closed
                };
                await client.Issue.Update(orgName, policyRepo, solvedIssue.Number, issueUpdate);
            }
        }

        private static Guid? GetFingerprint(string issueTitle)
        {
            var openParenthesis = issueTitle.LastIndexOf('(');
            var closeParenthesis = issueTitle.LastIndexOf(')');

            if (openParenthesis < 0 || closeParenthesis < 0 ||
                openParenthesis >= closeParenthesis ||
                closeParenthesis != issueTitle.Length - 1)
            {
                return null;
            }

            var length = closeParenthesis - openParenthesis + 1;
            var text = issueTitle.Substring(openParenthesis + 1, length - 2);
            if (Guid.TryParse(text, out var result))
                return result;

            return null;
        }
    }
}
