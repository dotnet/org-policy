using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class WhatIfCommand : ToolCommand
    {
        private string _orgName;
        private readonly List<string> _repoNames = new List<string>();
        private readonly List<string> _teamNames = new List<string>();
        private readonly List<string> _userNames = new List<string>();
        private string _outputFileName;
        private bool _viewInExcel;

        public override string Name => "what-if";

        public override string Description => "Shows the impact when a user is removed from a particular team";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r|repo=", "The {name} of the repo to analyze impact for", v => _repoNames.Add(v))
                   .Add("t|team=", "The {name} of the team to analyze impact for", v => _teamNames.Add(v))
                   .Add("u|user=", "The {name} of the user to analyze impact for", v => _userNames.Add(v))
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true);
        }

        public override async Task ExecuteAsync()
        {
            if (_outputFileName == null && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: you must specify an output path because you don't have Excel.");
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

            var repoFilter = CreateRepoFilter(org, _repoNames);
            var teamFilter = CreateTeamFilter(org, _teamNames);
            var userFilter = CreateUserFilter(org, _userNames);

            var emailByUser = new Dictionary<CachedUser, string>();
            var nameByUser = new Dictionary<CachedUser, string>();

            var csvDocument = new CsvDocument("team", "repo", "user", "change");

            using (var writer = csvDocument.Append())
            {
                foreach (var userAccess in org.Collaborators)
                {
                    var repo = userAccess.Repo;
                    var user = userAccess.User;

                    if (!repoFilter(repo) || !userFilter(user))
                        continue;

                    foreach (var team in org.Teams)
                    {
                        if (!teamFilter(team))
                            continue;

                        var whatIfRemoved = userAccess.WhatIfRemovedFromTeam(team);
                        var change = whatIfRemoved.ToString();

                        if (whatIfRemoved.IsUnchanged)
                            continue;

                        writer.WriteHyperlink(team.Url, team.Name, _viewInExcel);
                        writer.WriteHyperlink(repo.Url, repo.Name, _viewInExcel);
                        writer.WriteHyperlink(user.Url, user.Login, _viewInExcel);
                        writer.Write(change);
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
