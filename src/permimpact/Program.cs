using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PermissionImpact
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string orgName = null;
            string teamName = null;
            string outputFileName = null;
            string cacheLocation = null;
            var help = false;

            var options = new OptionSet()
                .Add("org=", "The {name} of the GitHub organization", v => orgName = v)
                .Add("team=", "The {name} of the team to analyze impact for", v => teamName = v)
                .Add("o|output=", "The {path} where the output .csv file should be written to.", v => outputFileName = v)
                .Add("cache-location=", "The {path} where the .json cache is located.", v => cacheLocation = v)
                .Add("h|?|help", null, v => help = true, true)
                .Add(new ResponseFileSource());

            try
            {
                var unprocessed = options.Parse(args);

                if (help)
                {
                    var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                    Console.Error.WriteLine($"This tool computes what would happen if a given team was removed.");
                    Console.Error.WriteLine();
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

            await RunAsync(orgName, teamName, outputFileName, cacheLocation);
        }

        private static async Task RunAsync(string orgName, string teamName, string outputFileName, string cacheLocation)
        {
            Console.WriteLine("Loading org data...");
            var cachedOrg = await CachedOrg.LoadFromCacheAsync(orgName, cacheLocation);
            if (cachedOrg == null)
            {
                Console.Error.WriteLine("The org wasn't loaded yet or the cache isn't valid anymore.");
                return;
            }

            var teamFilter = CreateTeamFilter(cachedOrg, teamName);

            Console.WriteLine("Loading Microsoft links...");
            var ospoClient = await OspoClientFactory.CreateAsync();
            var links = await ospoClient.GetAllAsync();

            Console.WriteLine("Computing result...");

            var emailByUser = new Dictionary<CachedUser, string>();
            var nameByUser = new Dictionary<CachedUser, string>();
            var microsoftLink = links.ToDictionary(l => l.GitHubInfo.Login);

            foreach (var user in cachedOrg.Users)
            {
                emailByUser[user] = user.Email;
                nameByUser[user] = user.Name;

                if (microsoftLink.TryGetValue(user.Login, out var link))
                {
                    emailByUser[user] = link.MicrosoftInfo.EmailAddress;
                    nameByUser[user] = link.MicrosoftInfo.PreferredName;
                }
            }

            var isForExcel = outputFileName == null;
            var csvDocument = new CsvDocument("team", "repo", "user", "user-name", "user-email", "change");

            using (var writer = csvDocument.Append())
            {
                foreach (var team in cachedOrg.Teams)
                {
                    if (teamFilter(team))
                        continue;

                    foreach (var repo in cachedOrg.Repos)
                    {
                        foreach (var userAccess in repo.Users)
                        {
                            var user = userAccess.User;
                            var whatIfRemoved = userAccess.WhatIfRemovedFromTeam(team);
                            var change = whatIfRemoved.ToString();

                            if (whatIfRemoved.IsUnchanged)
                                continue;

                            writer.WriteHyperlink(team.Url, team.Name, isForExcel);
                            writer.WriteHyperlink(repo.Url, repo.Name, isForExcel);
                            writer.WriteHyperlink(user.Url, user.Login, isForExcel);
                            writer.Write(nameByUser[user]);
                            writer.Write(emailByUser[user]);
                            writer.Write(change);
                            writer.WriteLine();
                        }
                    }
                }
            }

            if (outputFileName != null)
                csvDocument.Save(outputFileName);
            else
                csvDocument.ViewInExcel();
        }

        private static Func<CachedTeam, bool> CreateTeamFilter(CachedOrg cachedOrg, string filterText)
        {
            if (string.IsNullOrEmpty(filterText))
                return _ => true;

            var names = filterText.Split(';');
            var teamByName = cachedOrg.Teams.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            var includedTeams = new HashSet<CachedTeam>();
            foreach (var name in names)
            {
                if (!teamByName.TryGetValue(name, out var team))
                {
                    Console.Error.WriteLine($"warning: Team '{name}' doesn't exist");
                }
                else
                {
                    includedTeams.Add(team);
                }
            }

            return includedTeams.Contains;
        }
    }
}
