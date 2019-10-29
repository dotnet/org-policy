using System;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Office.Interop.Excel;
using Microsoft.Win32;

using Range = Microsoft.Office.Interop.Excel.Range;

namespace Microsoft.Csv
{
    public static class ExcelExtensions
    {
        public static bool IsExcelInstalled()
        {
            return Registry.ClassesRoot.OpenSubKey("Excel.Application") != null;
        }

        private static void AssertExcelIsInstalled()
        {
            if (!IsExcelInstalled())
                throw new NotSupportedException("You do not have Excel installed. Sorry.");
        }

        private static string GetTempCsvFile()
        {
            var tempFileName = Path.GetTempFileName();
            var csvFileName = Path.ChangeExtension(tempFileName, ".csv");
            File.Move(tempFileName, csvFileName);
            return csvFileName;
        }

        public static void ViewInExcel(this CsvDocument csvDocument)
        {
            AssertExcelIsInstalled();

            var a = new Application();
            try
            {
                a.DisplayAlerts = false;
                LoadCsvDocument(a, csvDocument);
            }
            finally
            {
                a.Visible = true;
                a.DisplayAlerts = true;
                Marshal.ReleaseComObject(a);
            }
        }

        public static void SaveToExcel(this CsvDocument csvDocument, string fileName)
        {
            AssertExcelIsInstalled();

            var a = new Application();
            try
            {
                a.DisplayAlerts = false;
                LoadCsvDocument(a, csvDocument);
                a.ActiveWorkbook.SaveAs(fileName, XlFileFormat.xlOpenXMLWorkbook, CreateBackup: false);
            }
            finally
            {
                a.Quit();
                Marshal.ReleaseComObject(a);
            }
        }

        private static void LoadCsvDocument(Application a, CsvDocument csvDocument)
        {
            ImportCsvDocument(a, csvDocument);
            FormatAsTable(a);
            SelectFirstCell(a);
        }

        private static void ImportCsvDocument(Application a, CsvDocument csvDocument)
        {
            var tempCsvFile = GetTempCsvFile();
            try
            {
                csvDocument.Save(tempCsvFile);

                var targetSheet = a.Workbooks.Add().ActiveSheet;

                a.ScreenUpdating = false;
                try
                {
                    var workbook = a.Workbooks.Open(tempCsvFile);
                    try
                    {
                        Range range = workbook.Sheets[1].Range("A1");
                        range.CurrentRegion.Copy(targetSheet.Range("A1"));
                    }
                    finally
                    {
                        workbook.Close(false);
                    }
                }
                finally
                {
                    a.ScreenUpdating = true;
                }
            }
            finally
            {
                File.Delete(tempCsvFile);
            }
        }

        private static void FormatAsTable(Application a)
        {
            SelectFirstCell(a);
            a.Range[a.Selection, a.Selection.End(XlDirection.xlToRight)].Select();
            a.Range[a.Selection, a.Selection.End(XlDirection.xlDown)].Select();

            var table = a.ActiveSheet.ListObjects.Add(XlListObjectHasHeaders: XlYesNoGuess.xlYes);
            table.Name = "Table1";
            table.TableStyle = "TableStyleLight2";
        }

        private static void SelectFirstCell(Application a)
        {
            a.Range["A1"].Select();
        }

        public static CsvDocument FromExcel(string fileName)
        {
            AssertExcelIsInstalled();

            var csvFileName = GetTempCsvFile();
            var xlsxFileName = fileName;

            try
            {
                ConvertToCsv(csvFileName, xlsxFileName);
                return CsvDocument.Load(csvFileName);
            }
            finally
            {
                File.Delete(csvFileName);
            }
        }

        private static void ConvertToCsv(string csvFileName, string xlsxFileName)
        {
            var a = new Application();
            try
            {
                a.DisplayAlerts = false;
                a.Workbooks.Open(xlsxFileName);
                a.ActiveWorkbook.SaveAs(csvFileName, XlFileFormat.xlCSV, CreateBackup: false);
                a.ActiveWorkbook.Close();
            }
            finally
            {
                // Close Excel
                a.Quit();
            }
        }

        public static void WriteHyperlink(this CsvWriter writer, string url, string text, bool useFormula = true)
        {
            text = useFormula
                    ? $"=HYPERLINK(\"{url}\", \"{text}\")"
                    : text;
            writer.Write(text);
        }
    }
}