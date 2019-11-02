using System;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class AuditCommand : ToolCommand
    {
        private string _orgName;
        private string _outputFileName;
        private bool _viewInExcel;

        public override string Name => "audit";

        public override string Description => "Produces a permission report for users and teams";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true);
        }

        public override async Task ExecuteAsync()
        {
            if (_orgName == null)
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var org = await CachedOrg.LoadFromCacheAsync(_orgName);

            if (org == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-refresh or cache-org first.");
                return;
            }

            var csvDocument = new CsvDocument("repo", "repo-state", "repo-last-pushed", "principal-kind", "principal", "permission", "via-team");
            using (var writer = csvDocument.Append())
            {
                foreach (var repo in org.Repos)
                {
                    var publicPrivate = repo.IsPrivate ? "private" : "public";
                    var lastPush = repo.LastPush.ToLocalTime().DateTime.ToString();

                    foreach (var teamAccess in repo.Teams)
                    {
                        var permissions = teamAccess.Permission.ToString().ToLower();
                        var teamName = teamAccess.Team.Name;
                        var teamUrl = teamAccess.Team.Url;

                        writer.WriteHyperlink(repo.Url, repo.Name, (bool)_viewInExcel);
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("team");
                        writer.WriteHyperlink(teamUrl, teamName, (bool)_viewInExcel);
                        writer.Write(permissions);
                        writer.Write(teamName);
                        writer.WriteLine();
                    }

                    foreach (var userAccess in repo.Users)
                    {
                        var via = userAccess.Describe().ToString();
                        var userUrl = CachedOrg.GetUserUrl(userAccess.UserLogin);
                        var permissions = userAccess.Permission.ToString().ToLower();

                        writer.WriteHyperlink(repo.Url, repo.Name, (bool)_viewInExcel);
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("user");
                        writer.WriteHyperlink(userUrl, userAccess.UserLogin, (bool)_viewInExcel);
                        writer.Write(permissions);
                        writer.Write(via);
                        writer.WriteLine();
                    }
                }
            }

            if (_outputFileName != null)
                csvDocument.Save(_outputFileName);

            if (_viewInExcel)
                csvDocument.ViewInExcel();
            else
                csvDocument.PrintToConsole();
        }
    }
}
