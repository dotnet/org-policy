using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Options;

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
            bool help = false;

            var options = new OptionSet()
                .Add("orgName", "The name of the GitHub organization", v => orgName = v)
                .Add("o|output=", "The {path} where the output .csv file should be written to.", v => outputFileName = v)
                .Add("cache-location=", "The {path} where the .json cache should be written to.", v => cacheLocation = v)
                .Add("github-token=", "The GitHub API {token} to be used.", v => githubToken = v)
                .Add("ospo-token=", "The OSPO API {token} to be used.", v => ospoToken = v)
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

            Console.WriteLine($"orgName = {orgName}");
            Console.WriteLine($"outputFileName = {outputFileName}");
            Console.WriteLine($"cacheLocation = {cacheLocation}");
            Console.WriteLine($"githubToken = {githubToken}");
            Console.WriteLine($"ospoToken = {ospoToken}");

            await RunAsync(orgName, outputFileName, cacheLocation, githubToken, ospoToken);
        }

        private static async Task RunAsync(string orgName, string outputFileName, string cacheLocation, string githubToken, string ospoToken)
        {
            var isForExcel = outputFileName == null;
            var gitHubClient = await GitHubClientFactory.CreateAsync(githubToken);
            var ospoClient = await OspoClientFactory.CreateAsync(ospoToken);
            var loader = new CachedOrgLoader(gitHubClient, Console.Out, cacheLocation, forceUpdate: false);
            var cachedOrg = await loader.LoadAsync(orgName);
            var userLinks = await MicrosoftUserLinks.LoadAsync(ospoClient);
            var context = new PolicyAnalysisContext(cachedOrg, userLinks);

            var csvDocument = new CsvDocument("org", "rule", "fingerprint", "violation", "repo", "user", "team", "receivers");
            using (var writer = csvDocument.Append())
            {
                var rules = GetRules();
                foreach (var rule in rules)
                {
                    var violations = rule.GetViolations(context);

                    foreach (var violation in violations)
                    {
                        writer.Write(orgName);
                        writer.Write(violation.DiagnosticId);
                        writer.Write(violation.Fingerprint.ToString());
                        writer.Write(violation.Message);

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

                        var receivers = string.Join(", ", violation.Receivers.Select(r => r.Login));
                        writer.Write(receivers);

                        writer.WriteLine();
                    }
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
