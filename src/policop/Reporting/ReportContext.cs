using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    // TODO: - create functions to filter early on repo, team, and user.
    //       - should combine term filters and column filters
    //       - create a single function called CreateRowFilter() that creates the combined filter for the row
    //       - fix WhatIfCommand
    internal sealed class ReportContext
    {
        private static readonly IReadOnlyList<string> _repoTermColumns = new[] { "r:name" };
        private static readonly IReadOnlyList<string> _teamTermColumns = new[] { "t:name", "t:full-name" };
        private static readonly IReadOnlyList<string> _userTermColumns = new[] { "u:login", "u:name", "u:email" };

        public List<string> RepoTerms { get; } = new List<string>();
        public List<string> TeamTerms { get; } = new List<string>();
        public List<string> UserTerms { get; } = new List<string>();
        public List<string> IncludedColumns { get; } = new List<string>();
        public List<string> ColumnFilters { get; } = new List<string>();

        public Func<CachedRepo, bool> CreateRepoFilter()
        {
            return CreateFilter<RepoReportColumn, CachedRepo>(RepoTerms, _repoTermColumns, (rc, r) => rc.GetValue(r));
        }

        public Func<CachedTeam, bool> CreateTeamFilter()
        {
            return CreateFilter<TeamReportColumn, CachedTeam>(TeamTerms, _teamTermColumns, (tc, t) => tc.GetValue(t));
        }

        public Func<CachedUser, bool> CreateUserFilter()
        {
            return CreateFilter<UserReportColumn, CachedUser>(UserTerms, _userTermColumns, (uc, u) => uc.GetValue(u));
        }

        private Func<TCache, bool> CreateFilter<TColumn, TCache>(IReadOnlyCollection<string> terms,
                                                                 IReadOnlyCollection<string> termColumns,
                                                                 Func<TColumn, TCache, string> valueSelector)
            where TColumn : ReportColumn
        {
            var termFilters = SelectedTypedColumns<TColumn>(ParseTermFilters(terms, termColumns));
            var columnFilters = SelectedTypedColumns<TColumn>(ParseColumnFilters(ColumnFilters));

            return r =>
            {
                if (termFilters.Count > 0)
                {
                    var anyTrue = false;

                    foreach (var f in termFilters)
                    {
                        var column = f.Key;
                        var term = f.Value;
                        var text = valueSelector(column, r) ?? string.Empty;
                        if (Evaluate(term, text))
                        {
                            anyTrue = true;
                            break;
                        }
                    }

                    if (!anyTrue)
                        return false;
                }

                foreach (var f in columnFilters)
                {
                    var column = f.Key;
                    var term = f.Value;
                    var text = valueSelector(column, r) ?? string.Empty;
                    if (!Evaluate(term, text))
                        return false;
                }

                return true;
            };
        }

        private static IReadOnlyList<KeyValuePair<TColumn, string>> SelectedTypedColumns<TColumn>(IReadOnlyCollection<KeyValuePair<ReportColumn, string>> genericFilters)
        {
            var filters = new List<KeyValuePair<TColumn, string>>();

            foreach (var kv in genericFilters)
            {
                var column = kv.Key;
                var expression = kv.Value;

                if (column is TColumn typedColumn)
                    filters.Add(new KeyValuePair<TColumn, string>(typedColumn, expression));
            }

            return filters.ToArray();
        }

        public Func<ReportRow, bool> CreateRowFilter()
        {
            var repoTermFilters = CreateTermFilters(RepoTerms, _repoTermColumns);
            var teamTermFilters = CreateTermFilters(TeamTerms, _teamTermColumns);
            var userTermFilters = CreateTermFilters(UserTerms, _userTermColumns);
            var columnFilters = CreateColumnFilters();

            return r => repoTermFilters(r) &&
                        teamTermFilters(r) &&
                        userTermFilters(r) &&
                        columnFilters(r);
        }

        public IReadOnlyList<ReportColumn> GetColumns(params string[] defaultColumns)
        {
            Debug.Assert(defaultColumns != null && defaultColumns.Length > 0);

            if (IncludedColumns.Count == 0)
                IncludedColumns.AddRange(defaultColumns);

            var result = new List<ReportColumn>();
            var hadErrors = false;

            foreach (var qualifiedName in IncludedColumns)
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

        private static Func<ReportRow, bool> CreateTermFilters(IReadOnlyCollection<string> expressions, IReadOnlyCollection<string> qualifiedNames)
        {
            var termFilters = ParseTermFilters(expressions, qualifiedNames);
            return CreateDisjunctionFilter(termFilters);
        }

        private Func<ReportRow, bool> CreateColumnFilters()
        {
            var termFilters = ParseColumnFilters(ColumnFilters);

            var hasErrors = termFilters.Any(kv => kv.Key == null);
            if (hasErrors)
                Environment.Exit(1);

            return CreateConjunctionFilter(termFilters);
        }

        private static IReadOnlyCollection<KeyValuePair<ReportColumn, string>> ParseTermFilters(IReadOnlyCollection<string> expressions, IReadOnlyCollection<string> qualifiedNames)
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
            return Evaluate(term, text);
        }

        private static bool Evaluate(string term, string text)
        {
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

        public CsvDocument CreateReport(IReadOnlyCollection<ReportRow> rows, IReadOnlyList<ReportColumn> columns)
        {
            if (rows.Count == 0 || columns.Count == 0)
                return new CsvDocument();

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

            return document;
        }
    }
}
