using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;
using Microsoft.DotnetOrg.PolicyCop.Reporting;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ListCommand : ToolCommand
    {
        private string _orgName;
        private bool _listRepos;
        private bool _listTeams;
        private bool _listUsers;
        private List<string> _activeTerms;
        private bool _viewInExcel;
        private readonly ReportContext _reportContext = new ReportContext();

        public override string Name => "list";

        public override string Description => "Lists repos, teams, users, team access or user access";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r", "Lists repos", v => { _listRepos = true; _activeTerms = _reportContext.RepoTerms; })
                   .Add("t", "Lists teams", v => { _listTeams = true; _activeTerms = _reportContext.TeamTerms; })
                   .Add("u", "Lists user", v => { _listUsers = true; _activeTerms = _reportContext.UserTerms; })
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("c", "Column names to include", v => _activeTerms = _reportContext.IncludedColumns)
                   .Add("f", "Extra filters", v => _activeTerms = _reportContext.ColumnFilters)
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
                Console.Error.WriteLine("error: links not cached yet. Run cache-refresh or cache-links first.");
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
                        ListUserAccess(org, linkSet);
                    }
                    else if (_listTeams && _listUsers)
                    {
                        ListTeamMembers(org, linkSet);
                    }
                    return;
                case 3:
                    Console.Error.WriteLine($"error: -r, -t, or -u cannot be specified all");
                    return;
            }
        }

        private void ListRepos(CachedOrg org)
        {
            var termFilters = _reportContext.CreateRepoTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();
            var rows = org.Repos
                          .OrderBy(r => r.Name)
                          .Select(r => new ReportRow(repo: r))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "r:private", "r:archived");
            OutputTable(rows, columns);
        }

        private void ListTeams(CachedOrg org)
        {
            var termFilters = _reportContext.CreateTeamTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();
            var rows = org.Teams
                          .OrderBy(t => t.GetFullName())
                          .Select(t => new ReportRow(team: t))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("t:name");
            OutputTable(rows, columns);
        }

        private void ListUsers(CachedOrg org, OspoLinkSet linkSet)
        {
            var termFilters = _reportContext.CreateUserTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();
            var rows = org.Users
                          .OrderBy(u => u.Login)
                          .Select(u => new ReportRow(user: u, linkSet: linkSet))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("u:login", "u:name", "u:ms-linked", "u:email");
            OutputTable(rows, columns);
        }

        private void ListTeamAccess(CachedOrg org)
        {
            var teamFilter = _reportContext.CreateTeamTermFilter();
            var repoFilter = _reportContext.CreateRepoTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();

            var rows = org.Teams
                          .SelectMany(t => t.Repos)
                          .Select(ta => new ReportRow(repo: ta.Repo, team: ta.Team, teamAccess: ta))
                          .Where(teamFilter)
                          .Where(repoFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "t:name", "ta:permission");
            OutputTable(rows, columns);
        }

        private void ListUserAccess(CachedOrg org, OspoLinkSet linkSet)
        {
            var userFilter = _reportContext.CreateUserTermFilter();
            var repoFilter = _reportContext.CreateRepoTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();

            var rows = org.Collaborators
                          .Select(c => new ReportRow(repo: c.Repo, user: c.User, userAccess: c, linkSet: linkSet))
                          .Where(userFilter)
                          .Where(repoFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "u:login", "ua:permission", "ua:reason");
            OutputTable(rows, columns);
        }

        private void ListTeamMembers(CachedOrg org, OspoLinkSet linkSet)
        {
            var teamFilter = _reportContext.CreateTeamTermFilter();
            var userFilter = _reportContext.CreateUserTermFilter();
            var columnFilters = _reportContext.CreateColumnFilters();

            var rows = org.Teams
                          .SelectMany(t => t.Members.Select(m => new ReportRow(team: t, user: m, linkSet: linkSet)))
                          .Where(teamFilter)
                          .Where(userFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = _reportContext.GetColumns("t:name", "u:login", "tm:maintainer");
            OutputTable(rows, columns);
        }

        private void OutputTable(IReadOnlyCollection<ReportRow> rows, IReadOnlyList<ReportColumn> columns)
        {
            var document = _reportContext.CreateReport(rows, columns);
            if (document.Keys.Count == 0 || document.Rows.Count == 0)
                return;

            if (_viewInExcel)
            {
                document.ViewInExcel();
            }
            else
            {
                document.PrintToConsole();
            }
        }
    }
}
