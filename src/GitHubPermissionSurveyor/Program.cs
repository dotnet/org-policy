using System;
using System.IO;
using System.Threading.Tasks;

using Terrajobst.Csv;
using Terrajobst.GitHubCaching;

namespace GitHubPermissionSurveyor
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

            var csvDocument = new CsvDocument("repo", "repo-state", "repo-last-pushed", "principal-kind", "principal", "permission", "via-team");
            using (var writer = csvDocument.Append())
            {
                foreach (var repo in cachedOrg.Repos)
                {
                    var publicPrivate = repo.IsPrivate ? "private" : "public";
                    var lastPush = repo.LastPush.ToLocalTime().DateTime.ToString();

                    foreach (var teamAccess in repo.Teams)
                    {
                        var permissions = teamAccess.Permission.ToString().ToLower();
                        var teamName = teamAccess.Team.Name;
                        var teamUrl = teamAccess.Team.Url;

                        writer.WriteHyperlink(repo.Url, repo.Name, isForExcel);
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("team");
                        writer.WriteHyperlink(teamUrl, teamName, isForExcel);
                        writer.Write(permissions);
                        writer.Write(teamName);
                        writer.WriteLine();
                    }

                    foreach (var userAccess in repo.Users)
                    {
                        var via = userAccess.Describe().ToString();
                        var userUrl = CachedOrg.GetUserUrl(userAccess.UserLogin);
                        var permissions = userAccess.Permission.ToString().ToLower();

                        writer.WriteHyperlink(repo.Url, repo.Name, isForExcel);
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("user");
                        writer.WriteHyperlink(userUrl, userAccess.UserLogin, isForExcel);
                        writer.Write(permissions);
                        writer.Write(via);
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
                var extension = Path.GetExtension(outputFileName);
                if (extension == ".md")
                    csvDocument.SaveAsMarkdownTable(outputFileName);
                else
                    csvDocument.Save(outputFileName);
            }
        }
    }
}
