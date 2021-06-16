using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class AuditActionsCommand : ToolCommand
    {
        private string? _orgName;
        private string? _outputFileName;
        private bool _viewInExcel;

        public override string Name => "audit-actions";

        public override string Description => "Creates a log that shows which actions are being used by which repo and workflow";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true);
        }

        public override async Task ExecuteAsync()
        {
            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var orgTasks = GetOrgNames().Select(n => (Name: n, Task: CacheManager.LoadOrgAsync(n)));
            
            foreach (var (orgName, orgTask) in orgTasks)
            {
                var org = await orgTask;
                if (org is null)
                {
                    Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-build or cache-org first.");
                    return;
                }
            }

            var orgs = orgTasks.Select(t => t.Task.Result!);

            var csvDocument = new CsvDocument("org", "repo", "workflow", "action-org", "action-repo", "action-version");
            using (var writer = csvDocument.Append())
            {
                foreach (var org in orgs)
                {
                    foreach (var repo in org.Repos)
                    {
                        foreach (var workflow in repo.Workflows)
                        {
                            foreach (var (actionOrg, actionRepo, actionVersion) in ParseActions(workflow.Contents))
                            {
                                writer.Write(org.Name);
                                writer.Write(repo.Name);
                                writer.Write(workflow.Name);
                                writer.Write(actionOrg);
                                writer.Write(actionRepo);
                                writer.Write(actionVersion);
                                writer.WriteLine();
                            }
                        }
                    }
                }
            }

            if (_outputFileName is not null)
                csvDocument.Save(_outputFileName);

            if (_viewInExcel)
                csvDocument.ViewInExcel();
            else
                csvDocument.PrintToConsole();
        }

        private IEnumerable<string> GetOrgNames()
        {
            if (string.IsNullOrEmpty(_orgName) || _orgName == "*")
                return CacheManager.GetCachedOrgNames();

            return new[] { _orgName! };
        }

        private static IEnumerable<(string ActionOrg, string ActionRepo, string ActionVersion)> ParseActions(string contents)
        {
            using var stringReader = new StringReader(contents);
            
            while (stringReader.ReadLine() is string line)
            {
                if (line.Contains("uses", StringComparison.OrdinalIgnoreCase))
                {
                    var indexOfColon = line.IndexOf(":");
                    if (indexOfColon >= 0)
                    {
                        var reference = line.Substring(indexOfColon + 1).Trim();
                        var indexOfAt = reference.IndexOf("@");
                        if (indexOfAt >= 0)
                        {
                            var orgAndRepo = reference.Substring(0, indexOfAt).Trim();
                            var version = reference.Substring(indexOfAt + 1).Trim();

                            var indexOfSlash = orgAndRepo.IndexOf("/");
                            if (indexOfSlash >= 0)
                            {
                                var org = orgAndRepo.Substring(0, indexOfSlash).Trim();
                                var repo = orgAndRepo.Substring(indexOfSlash + 1).Trim();
                                yield return (org, repo, version);
                            }
                        }
                    }
                }
            }
        }
    }
}
