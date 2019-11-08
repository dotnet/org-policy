using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
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
                   .Add("<>", v => _activeTerms?.Add(v));
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

            var org = await CacheManager.LoadOrgAsync(_orgName);

            if (org == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-build or cache-org first.");
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
                        ListUsers(org);
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
                    ListAuditMembers(org);
                    return;
            }
        }

        private void ListRepos(CachedOrg org)
        {
            var rows = org.Repos
                          .OrderBy(r => r.Name)
                          .Select(r => new ReportRow(repo: r))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "r:private", "r:archived");
            OutputTable(rows, columns);
        }

        private void ListTeams(CachedOrg org)
        {
            var rows = org.Teams
                          .OrderBy(t => t.GetFullName())
                          .Select(t => new ReportRow(team: t))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("t:full-name", "t:marker", "t:ms-owned", "t:description");
            OutputTable(rows, columns);
        }

        private void ListUsers(CachedOrg org)
        {
            var rows = org.Users
                          .OrderBy(u => u.Login)
                          .Select(u => new ReportRow(user: u))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("u:login", "u:name", "u:ms-linked", "u:email");
            OutputTable(rows, columns);
        }

        private void ListTeamAccess(CachedOrg org)
        {
            var rows = org.Teams
                          .SelectMany(t => t.Repos)
                          .Select(ta => new ReportRow(repo: ta.Repo, team: ta.Team, teamAccess: ta))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "t:name", "rt:permission");
            OutputTable(rows, columns);
        }

        private void ListUserAccess(CachedOrg org)
        {
            var rows = org.Collaborators
                          .Select(c => new ReportRow(repo: c.Repo, user: c.User, userAccess: c))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "u:login", "ru:permission", "ru:reason");
            OutputTable(rows, columns);
        }

        private void ListTeamMembers(CachedOrg org)
        {
            var rows = org.Teams
                          .SelectMany(t => t.Members.Select(m => new ReportRow(team: t, user: m)))
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("t:name", "u:login", "tu:maintainer");
            OutputTable(rows, columns);
        }

        private void ListAuditMembers(CachedOrg org)
        {
            var repoFilter = _reportContext.CreateRepoFilter();
            var teamFilter = _reportContext.CreateTeamFilter();
            var userFilter = _reportContext.CreateUserFilter();
            var rowFilter = _reportContext.CreateRowFilter();

            var teamRows = org.Repos
                              .Where(repoFilter)
                              .SelectMany(r => r.Teams.Where(ta => teamFilter(ta.Team)), (r, ta) => new ReportRow(repo: r, team: ta.Team, teamAccess: ta));
            var userRows = org.Repos
                              .Where(repoFilter)
                              .SelectMany(r => r.Users.Where(ua => userFilter(ua.User)), (r, ua) => new ReportRow(repo: r, user: ua.User, userAccess: ua));
            var rows = teamRows.Concat(userRows)
                               .Where(rowFilter)
                               .ToArray();

            var columns = _reportContext.GetColumns("r:name",
                                                    "r:private",
                                                    "r:last-push",
                                                    "rtu:principal-kind",
                                                    "rtu:principal",
                                                    "rtu:permission",
                                                    "ru:reason");
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
