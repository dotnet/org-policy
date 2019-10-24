using System;
using System.IO;

using Terrajobst.Csv;

namespace GitHubPermissionSurveyor
{
    internal static class MarkdownCsv
    {
        public static void SaveAsMarkdownTable(this CsvDocument document, string fileName)
        {
            var widths = new int[document.Keys.Count];

            for (var i = 0; i < document.Keys.Count; i++)
                widths[i] = document.Keys[i].Length;

            foreach (var row in document.Rows)
            {
                for (var i = 0; i < document.Keys.Count; i++)
                    widths[i] = Math.Max(widths[i], row[document.Keys[i]].Length);
            }

            using (var writer = File.CreateText(fileName))
            {
                {
                    writer.Write('|');
                    for (var i = 0; i < document.Keys.Count; i++)
                    {
                        var cellValue = document.Keys[i].PadRight(widths[i]);
                        writer.Write(cellValue);
                        writer.Write('|');
                    }
                    writer.WriteLine();
                }

                {
                    writer.Write('|');
                    for (var i = 0; i < document.Keys.Count; i++)
                    {
                        var separator = new string('-', widths[i]);
                        writer.Write(separator);
                        writer.Write('|');
                    }
                    writer.WriteLine();
                }

                foreach (var row in document.Rows)
                {
                    writer.Write('|');
                    for (var i = 0; i < document.Keys.Count; i++)
                    {
                        var cellValue = row[document.Keys[i]].PadRight(widths[i]);
                        writer.Write(cellValue);
                        writer.Write('|');
                    }
                    writer.WriteLine();
                }
            }
        }
    }
}
