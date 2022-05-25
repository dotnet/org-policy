using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.PolicyCop.Reporting;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ListCommand : ToolCommand
    {
        private string _orgName = "*";
        private bool _listRepos;
        private bool _listTeams;
        private bool _listUsers;
        private List<string>? _activeTerms;
        private bool _viewInExcel;
        private string? _outputFileName;
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
                   .Add("o|out=", "Specifies the output {path}", v => _outputFileName = v)
                   .Add("<>", v => _activeTerms?.Add(v));
        }

        private IEnumerable<string> GetOrgNames()
        {
            if (_orgName == "*")
                return CacheManager.GetCachedOrgNames();

            return new[] { _orgName! };
        }

        public override async Task ExecuteAsync()
        {
            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var orgNames = GetOrgNames();

            var orgTasks = orgNames.Select(o => CacheManager.LoadOrgAsync(o)).ToArray();
            await Task.WhenAll(orgTasks);

            var orgs = orgTasks.Where(t => t.Result is not null)
                               .Select(t => t.Result!)
                               .ToArray();

            if (orgs.Length == 1 && orgs.Length == 0)
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
                        ListRepos(orgs);
                    }
                    else if (_listTeams)
                    {
                        ListTeams(orgs);
                    }
                    else if (_listUsers)
                    {
                        ListUsers(orgs);
                    }
                    return;
                case 2:
                    if (_listRepos && _listTeams)
                    {
                        ListTeamAccess(orgs);
                    }
                    else if (_listRepos && _listUsers)
                    {
                        ListUserAccess(orgs);
                    }
                    else if (_listTeams && _listUsers)
                    {
                        ListTeamMembers(orgs);
                    }
                    return;
                case 3:
                    ListAuditMembers(orgs);
                    return;
            }
        }

        private void ListRepos(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Repos)
                           .OrderBy(r => r.Name)
                           .Select(r => new ReportRow(repo: r))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("r:name", "r:ms-owned", "r:private", "r:archived", "r:template", "r:description");
            OutputTable(rows, columns);
        }

        private void ListTeams(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Teams)
                           .OrderBy(t => t.GetFullName())
                           .Select(t => new ReportRow(team: t))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("t:full-slug", "t:marker", "t:ms-owned", "t:description");
            OutputTable(rows, columns);
        }

        private void ListUsers(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Users)
                           .OrderBy(u => u.Login)
                           .Select(u => new ReportRow(user: u))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("u:login", "u:name", "u:ms-linked", "u:email");
            OutputTable(rows, columns);
        }

        private void ListTeamAccess(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Teams)
                           .SelectMany(t => t.Repos)
                           .Select(ta => new ReportRow(repo: ta.Repo, team: ta.Team, teamAccess: ta))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("r:name", "t:slug", "rt:permission");
            OutputTable(rows, columns);
        }

        private void ListUserAccess(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Repos)
                           .SelectMany(r => r.EffectiveUsers)
                           .Select(ua => new ReportRow(repo: ua.Repo, user: ua.User, userAccess: ua))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("r:name", "u:login", "ru:permission", "ru:reason");
            OutputTable(rows, columns);
        }

        private void ListTeamMembers(IReadOnlyList<CachedOrg> orgs)
        {
            var rows = orgs.SelectMany(o => o.Teams)
                           .SelectMany(t => t.Members.Select(m => new ReportRow(team: t, user: m)))
                           .Where(_reportContext.CreateRowFilter())
                           .ToArray();

            var columns = GetColumns("t:slug", "u:login", "tu:maintainer");
            OutputTable(rows, columns);
        }

        private void ListAuditMembers(IReadOnlyList<CachedOrg> orgs)
        {
            var repoFilter = _reportContext.CreateRepoFilter();
            var teamFilter = _reportContext.CreateTeamFilter();
            var userFilter = _reportContext.CreateUserFilter();
            var rowFilter = _reportContext.CreateRowFilter();

            var teamRows = orgs.SelectMany(o => o.Repos)
                               .Where(repoFilter)
                               .SelectMany(r => r.Teams.Where(ta => teamFilter(ta.Team)), (r, ta) => new ReportRow(repo: r, team: ta.Team, teamAccess: ta));
            var userRows = orgs.SelectMany(o => o.Repos)
                               .Where(repoFilter)
                               .SelectMany(r => r.EffectiveUsers.Where(ua => userFilter(ua.User)), (r, ua) => new ReportRow(repo: r, user: ua.User, userAccess: ua));
            var rows = teamRows.Concat(userRows)
                               .Where(rowFilter)
                               .ToArray();

            var columns = GetColumns("r:name",
                                     "r:private",
                                     "r:last-push",
                                     "rtu:principal-kind",
                                     "rtu:principal",
                                     "rtu:permission",
                                     "ru:reason");
            OutputTable(rows, columns);
        }

        private IReadOnlyList<ReportColumn> GetColumns(params string[] names)
        {
            if (_orgName == "*")
                names = new[] { "o:name" }.Concat(names).ToArray();

            return _reportContext.GetColumns(names);
        }

        private void OutputTable(IReadOnlyCollection<ReportRow> rows, IReadOnlyList<ReportColumn> columns)
        {
            var document = _reportContext.CreateReport(rows, columns);
            if (document.Keys.Count == 0 || document.Rows.Count == 0)
                return;

            if (_outputFileName is not null)
                document.Save(_outputFileName);
            else if (_viewInExcel)
                document.ViewInExcel();
            else
                document.PrintToConsole();
        }
    }
}
