namespace Microsoft.Csv
{
    public class CsvTextReader : CsvReader
    {
        private readonly CsvLineReader _reader;
        private readonly IEnumerator<IEnumerable<string>> _enumerator;

        public CsvTextReader(TextReader textReader, CsvSettings settings)
            : base(settings)
        {
            if (textReader is null)
                throw new ArgumentNullException(nameof(textReader));

            _reader = new CsvLineReader(textReader, Settings);
            _enumerator = _reader.GetEnumerator();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _reader.Dispose();
        }

        public override IEnumerable<string>? Read()
        {
            if (!_enumerator.MoveNext())
                return null;

            var line = _enumerator.Current;
            return line;
        }
    }
}
