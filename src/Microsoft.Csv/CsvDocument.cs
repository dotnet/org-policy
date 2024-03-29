﻿using System.Collections.ObjectModel;

namespace Microsoft.Csv;

public class CsvDocument
{
    private readonly IList<string> _keys;
    private readonly IList<IDictionary<string, string>> _rows;

    public CsvDocument(params string[] keys)
        : this((IEnumerable<string>)keys)
    {
    }

    public CsvDocument(IEnumerable<string> keys)
        : this(keys.ToList(), new List<IDictionary<string, string>>())
    {
    }

    private CsvDocument(IList<string> keys, IList<IDictionary<string, string>> rows)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(rows);

        _keys = keys;
        _rows = rows;

        Keys = new ReadOnlyCollection<string>(_keys);
        Rows = new ReadOnlyCollection<IDictionary<string, string>>(rows);
    }

    public static CsvDocument Parse(string data)
    {
        return Load(data, CsvSettings.Default);
    }

    public static CsvDocument Parse(string data, CsvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!settings.IsValid)
            throw new ArgumentException(nameof(settings));

        using (var sr = new StringReader(data))
            return Load(sr, settings);
    }

    public static CsvDocument Load(string fileName)
    {
        return Load(fileName, CsvSettings.Default);
    }

    public static CsvDocument Load(string fileName, CsvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        if (!settings.IsValid)
            throw new ArgumentException(nameof(settings));

        using (var sr = new StreamReader(fileName, settings.Encoding))
            return Load(sr, settings);
    }

    public static CsvDocument Load(TextReader textReader)
    {
        return Load(textReader, CsvSettings.Default);
    }

    public static CsvDocument Load(TextReader textReader, CsvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        if (!settings.IsValid)
            throw new ArgumentException(nameof(settings));

        using (var reader = new CsvTextReader(textReader, settings))
            return Load(reader);
    }

    public static CsvDocument Load(CsvReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var rows = new List<IDictionary<string, string>>();
        var headerData = reader.Read();
        var headerArray = headerData is null
            ? new string[0]
            : headerData.ToArray();

        if (headerData is not null)
        {
            var rowData = reader.Read();
            while (rowData is not null)
            {
                var rowArray = rowData.ToArray();
                var count = Math.Min(headerArray.Length, rowArray.Length);

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < count; i++)
                {
                    var key = headerArray[i];
                    var value = rowArray[i];
                    row.Add(key, value);
                }

                for (var i = count; i < headerArray.Length; i++)
                {
                    var key = headerArray[i];
                    var value = string.Empty;
                    row.Add(key, value);
                }

                rows.Add(row);
                rowData = reader.Read();
            }
        }

        return new CsvDocument(headerArray, rows);
    }

    public void Save(string fileName)
    {
        Save(fileName, CsvSettings.Default);
    }

    public void Save(string fileName, CsvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var containingDirectory = Path.GetDirectoryName(fileName);
        if (containingDirectory is not null)
            Directory.CreateDirectory(containingDirectory);

        if (!settings.IsValid)
            throw new ArgumentException(nameof(settings));

        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var streamWriter = new StreamWriter(fileStream, settings.Encoding))
            Save(streamWriter, settings);
    }

    public void Save(TextWriter textWriter)
    {
        Save(textWriter, CsvSettings.Default);
    }

    public void Save(TextWriter textWriter, CsvSettings settings)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        if (!settings.IsValid)
            throw new ArgumentException(nameof(settings));

        using (var csvTextWriter = new CsvTextWriter(textWriter))
            Save(csvTextWriter);
    }

    public void Save(CsvWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var header = Keys;
        var rows = from r in Rows
            let v = from k in Keys
                select r.ContainsKey(k)
                    ? r[k]
                    : string.Empty
            select v;

        writer.WriteLine(header);
        foreach (var row in rows)
            writer.WriteLine(row);
    }

    public CsvReader CreateReader()
    {
        return new CsvDocumentReader(_keys, _rows);
    }

    public CsvWriter Append()
    {
        return new CsvDocumentWriter(_keys, _rows);
    }

    public ReadOnlyCollection<string> Keys { get; private set; }

    public ReadOnlyCollection<IDictionary<string, string>> Rows { get; private set; }
}