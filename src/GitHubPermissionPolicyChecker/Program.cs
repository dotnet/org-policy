using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Terrajobst.Csv;
using Terrajobst.GitHubCaching;
using Terrajobst.Ospo;

namespace GitHubPermissionPolicyChecker
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length < 1 || 2 < args.Length)
            {
                var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                Console.Error.WriteLine("error: wrong number of arguments");
                Console.Error.WriteLine($"usage {exeName} <org-name> [output-path]");
                return;
            }

            var orgName = args[0];
            var outputFileName = args.Length < 2 ? null : args[1];

            if (outputFileName == null && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: you must specify and output path because you don't have Excel.");
                return;
            }

            await RunAsync(orgName, outputFileName);
        }

        private static async Task RunAsync(string orgName, string outputFileName)
        {
            var isForExcel = outputFileName == null;
            var gitHubClient = await GitHubClientFactory.CreateAsync();
            var ospoClient = await OspoClientFactory.CreateAsync();
            var loader = new CachedOrgLoader(gitHubClient, Console.Out, forceUpdate: false);
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
