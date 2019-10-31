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

namespace Microsoft.DotnetOrg.PermissionImpact
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string orgName = null;
            var repoNames = new List<string>();
            var teamNames = new List<string>();
            var userNames = new List<string>();
            string outputFileName = null;
            string cacheLocation = null;
            var help = false;

            var options = new OptionSet()
                .Add("org=", "The {name} of the GitHub organization", v => orgName = v)
                .Add("r|repo=", "The {name} of the repo to analyze impact for", v => repoNames.Add(v))
                .Add("t|team=", "The {name} of the team to analyze impact for", v => teamNames.Add(v))
                .Add("u|user=", "The {name} of the user to analyze impact for", v => userNames.Add(v))
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


            await RunAsync(orgName, repoNames, teamNames, userNames, outputFileName, cacheLocation);
        }

        private static async Task RunAsync(string orgName,
                                           List<string> repoNames,
                                           List<string> teamNames,
                                           List<string> userNames,
                                           string outputFileName,
                                           string cacheLocation)
        {
            Console.WriteLine("Loading org data...");
            var cachedOrg = await CachedOrg.LoadFromCacheAsync(orgName, cacheLocation);
            if (cachedOrg == null)
            {
                Console.Error.WriteLine("The org wasn't loaded yet or the cache isn't valid anymore.");
                return;
            }

            var repoFilter = CreateRepoFilter(cachedOrg, repoNames);
            var teamFilter = CreateTeamFilter(cachedOrg, teamNames);
            var userFilter = CreateUserFilter(cachedOrg, userNames);

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
                    if (!string.IsNullOrEmpty(link.MicrosoftInfo.EmailAddress))
                        emailByUser[user] = link.MicrosoftInfo.EmailAddress;

                    if (!string.IsNullOrEmpty(link.MicrosoftInfo.PreferredName))
                        nameByUser[user] = link.MicrosoftInfo.PreferredName;
                }                
            }

            var isForExcel = outputFileName == null;
            var csvDocument = new CsvDocument("team", "repo", "user", "user-name", "user-email", "is-microsoft", "change", "repo-admins");

            using (var writer = csvDocument.Append())
            {
                foreach (var userAccess in cachedOrg.Collaborators)
                {
                    var repo = userAccess.Repo;
                    var user = userAccess.User;

                    if (!repoFilter(repo) || !userFilter(user))
                        continue;

                    foreach (var team in cachedOrg.Teams)
                    {
                        if (!teamFilter(team))
                            continue;

                        var whatIfRemoved = userAccess.WhatIfRemovedFromTeam(team);
                        var change = whatIfRemoved.ToString();

                        if (whatIfRemoved.IsUnchanged)
                            continue;

                        var isMicrosoft = microsoftLink.ContainsKey(user.Login) ? "Yes" : "No";
                        var repoAdmins = repo.GetAdministrators()
                                             .Select(u => (Email: emailByUser[u], Name: nameByUser[u]))
                                             .Where(t => !string.IsNullOrEmpty(t.Email))
                                             .Select(t => $"{t.Name}<{t.Email}>");
                        var repoAdminList = string.Join("; ", repoAdmins);

                        writer.WriteHyperlink(team.Url, team.Name, isForExcel);
                        writer.WriteHyperlink(repo.Url, repo.Name, isForExcel);
                        writer.WriteHyperlink(user.Url, user.Login, isForExcel);
                        writer.Write(nameByUser[user]);
                        writer.Write(emailByUser[user]);
                        writer.Write(isMicrosoft);
                        writer.Write(change);
                        writer.Write(repoAdminList);
                        writer.WriteLine();
                    }
                }
            }

            if (outputFileName != null)
                csvDocument.Save(outputFileName);
            else
                csvDocument.ViewInExcel();
        }

        private static Func<CachedRepo, bool> CreateRepoFilter(CachedOrg cachedOrg, List<string> names)
        {
            if (!names.Any())
                return _ => true;

            var repoByName = cachedOrg.Repos.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
            var includedRepos = new HashSet<CachedRepo>();
            foreach (var name in names)
            {
                if (!repoByName.TryGetValue(name, out var repo))
                {
                    Console.Error.WriteLine($"warning: Repo '{name}' doesn't exist");
                }
                else
                {
                    includedRepos.Add(repo);
                }
            }

            return includedRepos.Contains;
        }

        private static Func<CachedTeam, bool> CreateTeamFilter(CachedOrg cachedOrg, List<string> names)
        {
            if (!names.Any())
                return _ => true;

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

        private static Func<CachedUser, bool> CreateUserFilter(CachedOrg cachedOrg, List<string> logins)
        {
            if (!logins.Any())
                return _ => true;

            var userByLogin = cachedOrg.Users.ToDictionary(u => u.Login, StringComparer.OrdinalIgnoreCase);
            var includedUsers = new HashSet<CachedUser>();
            foreach (var login in logins)
            {
                if (!userByLogin.TryGetValue(login, out var user))
                {
                    Console.Error.WriteLine($"warning: User '{login}' doesn't exist");
                }
                else
                {
                    includedUsers.Add(user);
                }
            }

            return includedUsers.Contains;
        }
    }
}
