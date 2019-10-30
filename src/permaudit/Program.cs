using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PermissionAuditing
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            string orgName = null;
            string outputFileName = null;
            string cacheLocation = null;
            var help = false;

            var options = new OptionSet()
                .Add("org=", "The {name} of the GitHub organization", v => orgName = v)
                .Add("o|output=", "The {path} where the output .csv file should be written to.", v => outputFileName = v)
                .Add("cache-location=", "The {path} where the .json cache should be written to.", v => cacheLocation = v)
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

            await RunAsync(orgName, outputFileName, cacheLocation);
        }

        private static async Task RunAsync(string orgName, string outputFileName, string cacheLocation)
        {
            var isForExcel = outputFileName == null;

            var client = await GitHubClientFactory.CreateAsync();
            var cachedOrg = await CachedOrg.LoadAsync(client, orgName, Console.Out, cacheLocation, forceUpdate: false);

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
                csvDocument.Save(outputFileName);
            }
        }
    }
}
