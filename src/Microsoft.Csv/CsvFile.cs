namespace Microsoft.Csv
{
    public static class CsvFile
    {
        public static CsvTextReader Read(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            return Read(fileName, CsvSettings.Default);
        }

        public static CsvTextReader Read(string fileName, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var streamReader = new StreamReader(fileStream, settings.Encoding);
            return new CsvTextReader(streamReader, settings);
        }

        public static CsvTextWriter Create(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            return Create(fileName, CsvSettings.Default);
        }

        public static CsvTextWriter Create(string fileName, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            var streamWriter = new StreamWriter(fileStream, settings.Encoding);
            return new CsvTextWriter(streamWriter, settings);
        }

        public static CsvTextWriter Append(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            return Append(fileName, CsvSettings.Default);
        }

        public static CsvTextWriter Append(string fileName, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            var fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None);
            var streamWriter = new StreamWriter(fileStream, settings.Encoding);
            return new CsvTextWriter(streamWriter, settings);
        }

        public static IEnumerable<IEnumerable<string>> ReadLines(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            return ReadLines(fileName, CsvSettings.Default);
        }

        public static IEnumerable<IEnumerable<string>> ReadLines(string fileName, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            using (var csvReader = Read(fileName, settings))
            {
                var line = csvReader.Read();
                while (line is not null)
                {
                    yield return line;
                    line = csvReader.Read();
                }
            }
        }

        public static void WriteLines(string fileName, IEnumerable<IEnumerable<string>> lines)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            WriteLines(fileName, lines, CsvSettings.Default);
        }

        public static void WriteLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (header is null)
                throw new ArgumentNullException(nameof(header));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            WriteLines(fileName, header, lines, CsvSettings.Default);
        }

        public static void WriteLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (header is null)
                throw new ArgumentNullException(nameof(header));

            var headerLine = new[] { header };
            var allLines = headerLine.Concat(lines);
            WriteLines(fileName, allLines, settings);
        }

        public static void WriteLines(string fileName, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            using (var csvWriter = Create(fileName, settings))
            {
                foreach (var line in lines)
                    csvWriter.WriteLine(line);
            }
        }

        public static void AppendLines(string fileName, IEnumerable<IEnumerable<string>> lines)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            AppendLines(fileName, lines, CsvSettings.Default);
        }

        public static void AppendLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (header is null)
                throw new ArgumentNullException(nameof(header));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            AppendLines(fileName, header, lines, CsvSettings.Default);
        }

        public static void AppendLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (header is null)
                throw new ArgumentNullException(nameof(header));

            var headerLine = new[] { header };
            var allLines = headerLine.Concat(lines);
            AppendLines(fileName, allLines, settings);
        }

        public static void AppendLines(string fileName, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            using (var csvWriter = Append(fileName, settings))
            {
                foreach (var line in lines)
                    csvWriter.WriteLine(line);
            }
        }
    }
}