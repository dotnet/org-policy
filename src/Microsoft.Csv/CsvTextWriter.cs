using System.IO;
using System.Text;

namespace Microsoft.Csv
{
    public class CsvTextWriter : CsvWriter
    {
        private readonly TextWriter _textWriter;
        private bool _valuesSeen;
        private readonly char[] _textDelimiters;

        public CsvTextWriter(TextWriter textWriter)
            : this(textWriter, CsvSettings.Default)
        {
        }

        public CsvTextWriter(TextWriter textWriter, CsvSettings settings)
            : base(settings)
        {
            _textWriter = textWriter;
            _textDelimiters = new char[] { settings.Delimiter, settings.TextQualifier, '\r', '\n' };
        }

        public override CsvSettings Settings
        {
            get => base.Settings;
            set
            {
                base.Settings = value;
                _textDelimiters[0] = value.Delimiter;
                _textDelimiters[1] = value.TextQualifier;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _textWriter.Dispose();
        }

        public override void Write(string value)
        {
            if (_valuesSeen)
                _textWriter.Write(Settings.Delimiter);

            _valuesSeen = true;
            var escapedText = EscapeValue(value);
            _textWriter.Write(escapedText);
        }

        public override void WriteLine()
        {
            if (_valuesSeen)
            {
                _valuesSeen = false;
                _textWriter.WriteLine();
            }
        }

        protected string EscapeValue(string value)
        {
            if (value == null)
                return string.Empty;

            var textQualifier = Settings.TextQualifier;
            var needsEscaping = value.IndexOfAny(_textDelimiters) >= 0;
            if (!needsEscaping)
                return value;

            var sb = new StringBuilder(value.Length + 2);
            sb.Append(textQualifier);
            foreach (var c in value)
            {
                if (c == textQualifier)
                    sb.Append(textQualifier);

                sb.Append(c);
            }
            sb.Append(textQualifier);
            return sb.ToString();
        }
    }
}
