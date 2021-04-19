using System;
using System.Collections.Generic;

namespace Microsoft.Csv
{
    public abstract class CsvWriter : IDisposable
    {
        protected CsvWriter(CsvSettings settings)
        {
            Settings = settings;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public abstract void Write(string value);

        public virtual void Write(IEnumerable<string> values)
        {
            foreach (var value in values)
                Write(value);
        }

        public abstract void WriteLine();

        public virtual void WriteLine(IEnumerable<string> values)
        {
            foreach (var value in values)
                Write(value);

            WriteLine();
        }

        public virtual CsvSettings Settings { get; set; }
    }
}