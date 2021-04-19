using System;
using System.Text;

namespace Microsoft.Csv
{
    public readonly struct CsvSettings
    {
        public static CsvSettings Default { get; } = new CsvSettings(
            encoding: Encoding.UTF8,
            delimiter: ',',
            textQualifier: '"'
        );

        public CsvSettings(Encoding encoding, char delimiter, char textQualifier)
            : this()
        {
            if (encoding is null)
                throw new ArgumentNullException(nameof(encoding));

            Encoding = encoding;
            Delimiter = delimiter;
            TextQualifier = textQualifier;
        }

        public Encoding Encoding { get; }
        public char Delimiter { get; }
        public char TextQualifier { get; }

        public bool IsValid => Encoding is not null;
    }
}