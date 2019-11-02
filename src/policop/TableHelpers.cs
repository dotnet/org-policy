using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Csv;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal static class TableHelpers
    {
        public static void PrintToConsole<T>(this IReadOnlyCollection<T> rows, string[] headers) where T : ITuple
        {
            rows.ToCsvDocument(headers).PrintToConsole();
        }

        public static void PrintToConsole(this CsvDocument document)
        {
            var indent = "    ";
            var columnWidths = new int[document.Keys.Count];

            for (var i = 0; i < columnWidths.Length; i++)
                columnWidths[i] = document.Keys[i].Length;

            foreach (var row in document.Rows)
            {
                for (var i = 0; i < columnWidths.Length; i++)
                {
                    var key = document.Keys[i];
                    var text = row[key];
                    columnWidths[i] = Math.Max(columnWidths[i], text.Length);
                }
            }

            for (var i = 0; i < columnWidths.Length; i++)
            {
                Console.Write(indent);

                var text = document.Keys[i];
                Console.Write(text.PadRight(columnWidths[i]));
            }

            Console.WriteLine();

            for (var i = 0; i < columnWidths.Length; i++)
            {
                Console.Write(indent);

                var text = new string('-', columnWidths[i]);
                Console.Write(text);
            }

            Console.WriteLine();

            foreach (var row in document.Rows)
            {
                for (var i = 0; i < columnWidths.Length; i++)
                {
                    Console.Write(indent);

                    var key = document.Keys[i];
                    var text = row[key];
                    Console.Write(text.PadRight(columnWidths[i]));
                }

                Console.WriteLine();
            }
        }

        public static CsvDocument ToCsvDocument<T>(this IEnumerable<T> rows, params string[] headers)
            where T : ITuple
        {
            var document = new CsvDocument(headers);

            using (var writer = document.Append())
            {
                foreach (var row in rows)
                {
                    for (var i = 0; i < row.Length; i++)
                        writer.Write(Convert.ToString(row[i]));

                    writer.WriteLine();
                }
            }

            return document;
        }
    }
}