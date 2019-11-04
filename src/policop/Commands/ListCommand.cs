using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly List<string> _repoTerms = new List<string>();
        private readonly List<string> _teamTerms = new List<string>();
        private readonly List<string> _userTerms = new List<string>();
        private readonly List<string> _includedColumns = new List<string>();
        private readonly List<string> _columnFilters = new List<string>();
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
                   .Add("c", "Column names to include", v => _activeTerms = _includedColumns)
                   .Add("f", "Extra filters", v => _activeTerms = _columnFilters)
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
            var termFilters = CreateRepoTermFilter();
            var columnFilters = CreateColumnFilters();
            var rows = org.Repos
                          .OrderBy(r => r.Name)
                          .Select(r => new ReportRow(repo: r))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("r:name", "r:private", "r:archived");
            OutputTable(rows, columns);
        }

        private void ListTeams(CachedOrg org)
        {
            var termFilters = CreateTeamTermFilter();
            var columnFilters = CreateColumnFilters();
            var rows = org.Teams
                          .OrderBy(t => t.GetFullName())
                          .Select(t => new ReportRow(team: t))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("t:name");
            OutputTable(rows, columns);
        }

        private void ListUsers(CachedOrg org, OspoLinkSet linkSet)
        {
            var termFilters = CreateUserTermFilter();
            var columnFilters = CreateColumnFilters();
            var rows = org.Users
                          .OrderBy(u => u.Login)
                          .Select(u => new ReportRow(user: u, linkSet: linkSet))
                          .Where(termFilters)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("u:login", "u:name", "u:ms-linked", "u:email");
            OutputTable(rows, columns);
        }

        private void ListTeamAccess(CachedOrg org)
        {
            var teamFilter = CreateTeamTermFilter();
            var repoFilter = CreateRepoTermFilter();
            var columnFilters = CreateColumnFilters();

            var rows = org.Teams
                          .SelectMany(t => t.Repos)
                          .Select(ta => new ReportRow(repo: ta.Repo, team: ta.Team, teamAccess: ta))
                          .Where(teamFilter)
                          .Where(repoFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("r:name", "t:name", "ta:permission");
            OutputTable(rows, columns);
        }

        private void ListUserAccess(CachedOrg org, OspoLinkSet linkSet)
        {
            var userFilter = CreateUserTermFilter();
            var repoFilter = CreateRepoTermFilter();
            var columnFilters = CreateColumnFilters();

            var rows = org.Collaborators
                          .Select(c => new ReportRow(repo: c.Repo, user: c.User, userAccess: c, linkSet: linkSet))
                          .Where(userFilter)
                          .Where(repoFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("r:name", "u:login", "ua:permission", "ua:reason");
            OutputTable(rows, columns);
        }

        private void ListTeamMembers(CachedOrg org, OspoLinkSet linkSet)
        {
            var teamFilter = CreateTeamTermFilter();
            var userFilter = CreateUserTermFilter();
            var columnFilters = CreateColumnFilters();

            var rows = org.Teams
                          .SelectMany(t => t.Members.Select(m => new ReportRow(team: t, user: m, linkSet: linkSet)))
                          .Where(teamFilter)
                          .Where(userFilter)
                          .Where(columnFilters)
                          .ToArray();

            var columns = GetColumns("t:name", "u:login", "tm:maintainer");
            OutputTable(rows, columns);
        }

        private Func<ReportRow, bool> CreateRepoTermFilter()
        {
            return CreateTermFilters(_repoTerms, "r:name");
        }

        private Func<ReportRow, bool> CreateTeamTermFilter()
        {
            return CreateTermFilters(_teamTerms, "t:name", "t:full-name");
        }

        private Func<ReportRow, bool> CreateUserTermFilter()
        {
            return CreateTermFilters(_userTerms, "u:login", "u:name", "u:email");
        }

        private IReadOnlyList<ReportColumn> GetColumns(params string[] defaultColumns)
        {
            Debug.Assert(defaultColumns != null && defaultColumns.Length > 0);

            if (_includedColumns.Count == 0)
                _includedColumns.AddRange(defaultColumns);

            var result = new List<ReportColumn>();
            var hadErrors = false;

            foreach (var qualifiedName in _includedColumns)
            {
                var column = ReportColumn.Get(qualifiedName);
                if (column != null)
                {
                    result.Add(column);
                }
                else
                {
                    Console.Error.WriteLine($"error: column '{qualifiedName}' isn't valid");
                    hadErrors = true;
                }
            }

            if (hadErrors)
                Environment.Exit(1);

            return result;
        }

        private static Func<ReportRow, bool> CreateTermFilters(IReadOnlyCollection<string> expressions, params string[] qualifiedNames)
        {
            var termFilters = ParseTermFilters(expressions, qualifiedNames);
            return CreateDisjunctionFilter(termFilters);
        }

        private Func<ReportRow, bool> CreateColumnFilters()
        {
            var termFilters = ParseColumnFilters(_columnFilters);

            var hasErrors = termFilters.Any(kv => kv.Key == null);
            if (hasErrors)
                Environment.Exit(1);

            return CreateConjunctionFilter(termFilters);
        }

        private static IReadOnlyCollection<KeyValuePair<ReportColumn, string>> ParseTermFilters(IReadOnlyCollection<string> expressions, string[] qualifiedNames)
        {
            var columns = new List<ReportColumn>();

            foreach (var qualifiedName in qualifiedNames)
            {
                var column = ReportColumn.Get(qualifiedName);
                Debug.Assert(column != null, $"Column {qualifiedName} is invalid");
                columns.Add(column);
            }

            var result = new List<KeyValuePair<ReportColumn, string>>();

            foreach (var column in columns)
            {
                foreach (var expression in expressions)
                {
                    result.Add(new KeyValuePair<ReportColumn, string>(column, expression));
                }
            }

            return result;
        }

        private static IReadOnlyCollection<KeyValuePair<ReportColumn, string>> ParseColumnFilters(IReadOnlyCollection<string> expressions)
        {
            return expressions.Select(ParseColumnFilter).ToArray();
        }

        private static KeyValuePair<ReportColumn, string> ParseColumnFilter(string expression)
        {
            var indexOfEquals = expression.IndexOf("=");

            string qualifiedName;
            string value;

            if (indexOfEquals < 0)
            {
                qualifiedName = expression.Trim();
                value = "Yes";
            }
            else
            {
                qualifiedName = expression.Substring(0, indexOfEquals).Trim();
                value = expression.Substring(indexOfEquals + 1).Trim();
            }

            var column = ReportColumn.Get(qualifiedName);
            if (column == null)
                Console.Error.WriteLine($"error: column '{qualifiedName}' isn't valid");

            return new KeyValuePair<ReportColumn, string>(column, value);
        }

        private static Func<ReportRow, bool> CreateConjunctionFilter(IReadOnlyCollection<KeyValuePair<ReportColumn, string>> columnFilters)
        {
            if (columnFilters.Count == 0)
                return _ => true;

            return row =>
            {
                foreach (var columnFilter in columnFilters)
                {
                    if (!Evaluate(row, columnFilter))
                        return false;
                }

                return true;
            };
        }

        private static Func<ReportRow, bool> CreateDisjunctionFilter(IReadOnlyCollection<KeyValuePair<ReportColumn, string>> columnFilters)
        {
            if (columnFilters.Count == 0)
                return _ => true;

            return row =>
            {
                foreach (var columnFilter in columnFilters)
                {
                    if (Evaluate(row, columnFilter))
                        return true;
                }

                return false;
            };
        }

        private static bool Evaluate(ReportRow row, KeyValuePair<ReportColumn, string> columnFilter)
        {
            var column = columnFilter.Key;
            var term = columnFilter.Value;
            var text = column.GetValue(row) ?? string.Empty;

            var wildcardAtStart = term.StartsWith("*");
            var wildcardAtEnd = term.EndsWith("*");
            var actualTerm = term.Trim('*');

            if (wildcardAtStart && wildcardAtEnd)
                return text.IndexOf(actualTerm, StringComparison.OrdinalIgnoreCase) >= 0;

            if (wildcardAtStart)
                return text.EndsWith(actualTerm, StringComparison.OrdinalIgnoreCase);

            if (wildcardAtEnd)
                return text.StartsWith(actualTerm, StringComparison.OrdinalIgnoreCase);

            return text.Equals(actualTerm, StringComparison.OrdinalIgnoreCase);
        }

        private void OutputTable(IReadOnlyCollection<ReportRow> rows, IReadOnlyList<ReportColumn> columns)
        {
            if (rows.Count == 0 || columns.Count == 0)
                return;

            var headers = columns.Select(h => h.QualifiedName);
            var document = new CsvDocument(headers);

            using (var writer = document.Append())
            {
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        var value = column.GetValue(row);
                        writer.Write(value ?? string.Empty);
                    }

                    writer.WriteLine();
                }
            }

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
