using System;
using System.Collections.Generic;

namespace Microsoft.Csv
{
    public abstract class CsvReader : IDisposable
    {
        protected CsvReader(CsvSettings settings)
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

        public abstract IEnumerable<string> Read();

        public CsvSettings Settings { get; set; }
    }
}