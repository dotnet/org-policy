namespace Microsoft.Csv
{
    public static class CsvFile
    {
        public static CsvTextReader Read(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            return Read(fileName, CsvSettings.Default);
        }

        public static CsvTextReader Read(string fileName, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var streamReader = new StreamReader(fileStream, settings.Encoding);
            return new CsvTextReader(streamReader, settings);
        }

        public static CsvTextWriter Create(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            return Create(fileName, CsvSettings.Default);
        }

        public static CsvTextWriter Create(string fileName, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            var streamWriter = new StreamWriter(fileStream, settings.Encoding);
            return new CsvTextWriter(streamWriter, settings);
        }

        public static CsvTextWriter Append(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            return Append(fileName, CsvSettings.Default);
        }

        public static CsvTextWriter Append(string fileName, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            var fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None);
            var streamWriter = new StreamWriter(fileStream, settings.Encoding);
            return new CsvTextWriter(streamWriter, settings);
        }

        public static IEnumerable<IEnumerable<string>> ReadLines(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            return ReadLines(fileName, CsvSettings.Default);
        }

        public static IEnumerable<IEnumerable<string>> ReadLines(string fileName, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);

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
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(lines);

            WriteLines(fileName, lines, CsvSettings.Default);
        }

        public static void WriteLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(header);
            ArgumentNullException.ThrowIfNull(lines);

            WriteLines(fileName, header, lines, CsvSettings.Default);
        }

        public static void WriteLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(header);

            var headerLine = new[] { header };
            var allLines = headerLine.Concat(lines);
            WriteLines(fileName, allLines, settings);
        }

        public static void WriteLines(string fileName, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(lines);

            using (var csvWriter = Create(fileName, settings))
            {
                foreach (var line in lines)
                    csvWriter.WriteLine(line);
            }
        }

        public static void AppendLines(string fileName, IEnumerable<IEnumerable<string>> lines)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(lines);

            AppendLines(fileName, lines, CsvSettings.Default);
        }

        public static void AppendLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(header);
            ArgumentNullException.ThrowIfNull(lines);

            AppendLines(fileName, header, lines, CsvSettings.Default);
        }

        public static void AppendLines(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(header);

            var headerLine = new[] { header };
            var allLines = headerLine.Concat(lines);
            AppendLines(fileName, allLines, settings);
        }

        public static void AppendLines(string fileName, IEnumerable<IEnumerable<string>> lines, CsvSettings settings)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(lines);

            using (var csvWriter = Append(fileName, settings))
            {
                foreach (var line in lines)
                    csvWriter.WriteLine(line);
            }
        }
    }
}