using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Csv;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    // TODO: - create functions to filter early on repo, team, and user.
    //       - should combine term filters and column filters
    //       - create a single function called CreateRowFilter() that creates the combined filter for the row
    //       - fix WhatIfCommand
    internal sealed class ReportContext
    {
        public List<string> RepoTerms { get; } = new List<string>();
        public List<string> TeamTerms { get; } = new List<string>();
        public List<string> UserTerms { get; } = new List<string>();
        public List<string> IncludedColumns { get; } = new List<string>();
        public List<string> ColumnFilters { get; } = new List<string>();

        public Func<ReportRow, bool> CreateRepoTermFilter()
        {
            return CreateTermFilters(RepoTerms, "r:name");
        }

        public Func<ReportRow, bool> CreateTeamTermFilter()
        {
            return CreateTermFilters(TeamTerms, "t:name", "t:full-name");
        }

        public Func<ReportRow, bool> CreateUserTermFilter()
        {
            return CreateTermFilters(UserTerms, "u:login", "u:name", "u:email");
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

        private static Func<ReportRow, bool> CreateTermFilters(IReadOnlyCollection<string> expressions, params string[] qualifiedNames)
        {
            var termFilters = ParseTermFilters(expressions, qualifiedNames);
            return CreateDisjunctionFilter(termFilters);
        }

        public Func<ReportRow, bool> CreateColumnFilters()
        {
            var termFilters = ParseColumnFilters(ColumnFilters);

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
