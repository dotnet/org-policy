using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ListCommand : ToolCommand
    {
        private string _orgName;
        private bool _listRepos;
        private bool _listTeams;
        private bool _listUsers;
        private readonly List<string> _repoTerms = new List<string>();
        private readonly List<string> _teamTerms = new List<string>();
        private readonly List<string> _userTerms = new List<string>();
        private List<string> _activeTerms;
        private bool _viewInExcel;

        public override string Name => "list";

        public override string Description => "Lists repos, teams, users, team access or user access";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r", "Lists repos", v => { _listRepos = true; _activeTerms = _repoTerms; })
                   .Add("t", "Lists teams", v => { _listTeams = true; _activeTerms = _teamTerms; })
                   .Add("u", "Lists user", v => { _listUsers = true; _activeTerms = _userTerms; })
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("<>", v => _activeTerms.Add(v));
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
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

            var linkSet = await OspoLinkSet.LoadFromCacheAsync();

            if (linkSet == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-refresh or cache-links first.");
                return;
            }

            var count = (_listRepos ? 1 : 0) + (_listTeams ? 1 : 0) + (_listUsers ? 1 : 0);
            switch (count)
            {
                case 0:
                    Console.Error.WriteLine($"error: -r, -t, or -u must be specified");
                    return;
                case 1:
                    if (_listRepos)
                    {
                        ListRepos(org);
                    }
                    else if (_listTeams)
                    {
                        ListTeams(org);
                    }
                    else if (_listUsers)
                    {
                        ListUsers(org, linkSet);
                    }
                    return;
                case 2:
                    if (_listRepos && _listTeams)
                    {
                        ListTeamAccess(org);
                    }
                    else if (_listRepos && _listUsers)
                    {
                        ListUserAccess(org);
                    }
                    else if (_listTeams && _listUsers)
                    {
                        ListTeamMembers(org);
                    }
                    return;
                case 3:
                    Console.Error.WriteLine($"error: -r, -t, or -u cannot be specified all");
                    return;
            }
        }

        private void ListRepos(CachedOrg org)
        {
            var repoFilter = CreateRepoFilter();
            var rows = org.Repos
                          .Where(repoFilter)
                          .OrderBy(r => r.Name)
                          .Select(r => (r.Name, r.IsPrivate ? "Private" : "Public", r.IsArchived ? "Yes" : "No", r.LastPush))
                          .ToArray();

            OutputTable(rows, "repo", "visibility", "archived", "last-push");
        }

        private void ListTeams(CachedOrg org)
        {
            var teamFilter = CreateTeamFilter();
            var rows = org.Teams
                          .Where(teamFilter)
                          .OrderBy(t => t.GetFullName())
                          .Select(t => ValueTuple.Create(t.GetFullName()))
                          .ToArray();

            OutputTable(rows, "team");
        }

        private void ListUsers(CachedOrg org, OspoLinkSet linkSet)
        {
            var userFilter = CreateUserFilter();
            var rows = org.Users
                          .Where(userFilter)
                          .OrderBy(u => u.Login)
                          .Select(u =>
                          {
                              var kind = u.IsExternal
                                          ? "External"
                                          : u.IsOwner
                                              ? "Owner"
                                              : "Member";

                              if (linkSet.LinkByLogin.TryGetValue(u.Login, out var link))
                                  return (User: u, Kind: kind, Linked: true, Email: link.MicrosoftInfo.EmailAddress, Name: link.MicrosoftInfo.PreferredName);
                              else
                                  return (User: u, Kind: kind, Linked: false, u.Email, u.Name);
                          })
                          .Select(t => (t.User.Login, t.Kind, t.Linked ? "Yes" : "No", t.Email, t.Name))
                          .ToArray();

            OutputTable(rows, "user-login", "kind", "linked", "email", "name");
        }

        private void ListTeamAccess(CachedOrg org)
        {
            var teamFilter = CreateTeamFilter();
            var repoFilter = CreateRepoFilter();

            var rows = org.Teams
                          .Where(teamFilter)
                          .SelectMany(t => t.Repos.Where(ta => repoFilter(ta.Repo)))
                          .Select(ta =>
                          {
                              var team = ta.Team;
                              var repo = ta.Repo;
                              var permission = ta.Permission.ToString().ToLower();
                              return (repo.Name, team.GetFullName(), permission);
                          })
                          .ToArray();

            OutputTable(rows, "repo", "team", "permission");
        }

        private void ListUserAccess(CachedOrg org)
        {
            var userFilter = CreateUserFilter();
            var repoFilter = CreateRepoFilter();

            var rows = org.Collaborators
                          .Where(c => userFilter(c.User) && repoFilter(c.Repo))
                          .Select(c =>
                          {
                              var user = c.User;
                              var repo = c.Repo;
                              var permission = c.Permission.ToString().ToLower();
                              var reason = c.Describe();
                              return (repo.Name, user.Login, permission, reason);
                          })
                          .ToArray();

            OutputTable(rows, "repo", "user-login", "permission", "reason");
        }

        private void ListTeamMembers(CachedOrg org)
        {
            var teamFilter = CreateTeamFilter();
            var userFilter = CreateUserFilter();

            var rows = org.Teams
                          .Where(teamFilter)
                          .SelectMany(t => t.Members.Select(m => (Team: t, User: m, Kind: t.Maintainers.Contains(m) ? "Maintainer" : "Member")))
                          .Where(t => userFilter(t.User))
                          .Select(t => (t.Team.GetFullName(), t.User.Login, t.Kind))
                          .ToArray();

            OutputTable(rows, "team", "user-login", "kind");
        }

        private Func<CachedRepo, bool> CreateRepoFilter()
        {
            return CreateFilter<CachedRepo>(r => r.Name, _repoTerms);
        }

        private Func<CachedTeam, bool> CreateTeamFilter()
        {
            if (_teamTerms.Count == 0)
                return _ => true;

            var filters = new[]
            {
                CreateFilter<CachedTeam>(t => t.Name, _teamTerms),
                CreateFilter<CachedTeam>(t => t.GetFullName(), _teamTerms)
            };

            return t => filters.Any(f => f(t));
        }

        private Func<CachedUser, bool> CreateUserFilter()
        {
            if (_userTerms.Count == 0)
                return _ => true;

            var filters = new[]
            {
                CreateFilter<CachedUser>(t => t.Login, _userTerms),
                CreateFilter<CachedUser>(t => t.Name, _userTerms),
                CreateFilter<CachedUser>(t => t.Email, _userTerms)
            };

            return u => filters.Any(f => f(u));
        }

        public static Func<T, bool> CreateFilter<T>(Func<T, string> selector, List<string> terms)
        {
            if (terms.Count == 0)
                return _ => true;

            return item =>
            {
                var text = selector(item) ?? string.Empty;

                foreach (var term in terms)
                {
                    var wildcardAtStart = term.StartsWith("*");
                    var wildcardAtEnd = term.EndsWith("*");
                    var actualTerm = term.Trim('*');

                    bool result;

                    if (wildcardAtStart && wildcardAtEnd)
                    {
                        result = text.IndexOf(actualTerm, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else if (wildcardAtStart)
                    {
                        result = text.EndsWith(actualTerm, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (wildcardAtEnd)
                    {
                        result = text.StartsWith(actualTerm, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        result = text.Equals(actualTerm, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!result)
                        return false;
                }
                return true;
            };
        }

        private void OutputTable<T>(IReadOnlyCollection<T> rows, params string[] headers) where T : ITuple
        {
            if (rows.Count == 0)
                return;

            if (_viewInExcel)
            {
                var document = rows.ToCsvDocument(headers);
                document.ViewInExcel();
            }
            else
            {
                rows.PrintToConsole(headers);
            }
        }
    }
}
