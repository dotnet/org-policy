using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using GitHubPermissionPolicyChecker.Rules;

using Terrajobst.Csv;
using Terrajobst.GitHubCaching;

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
            var client = await GitHubClientFactory.CreateAsync();
            var loader = new CachedOrgLoader(client, Console.Out, forceUpdate: false);
            var cachedOrg = await loader.LoadAsync(orgName);

            var csvDocument = new CsvDocument("org", "rule", "violation", "repo", "user", "team");
            using (var writer = csvDocument.Append())
            {
                var rules = GetRules();
                foreach (var rule in rules)
                {
                    var ruleName = rule.GetType().Name;
                    var violations = rule.GetViolations(cachedOrg);

                    foreach (var violation in violations)
                    {
                        writer.Write(orgName);
                        writer.Write(ruleName);
                        writer.Write(violation.Message);

                        if (violation.Repo == null)
                            writer.Write(string.Empty);
                        else
                            writer.WriteHyperlink(violation.Repo.Url, violation.Repo.Name, isForExcel);

                        if (violation.User == null)
                        {
                            writer.Write(string.Empty);
                        }
                        else
                        {
                            var url = CachedOrg.GetUserUrl(violation.User);
                            writer.WriteHyperlink(url, violation.User, isForExcel);
                        }

                        if (violation.Team == null)
                            writer.Write(string.Empty);
                        else
                            writer.WriteHyperlink(violation.Team.Url, violation.Team.Name, isForExcel);

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
