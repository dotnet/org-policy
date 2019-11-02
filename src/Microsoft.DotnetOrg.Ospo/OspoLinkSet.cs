using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.Ospo
{
    public sealed class OspoLinkSet
    {
        public OspoLinkSet()
        {
        }

        public void Initialize()
        {
            LinkByLogin = Links.ToDictionary(l => l.GitHubInfo.Login);
        }

        public IReadOnlyList<OspoLink> Links { get; set; } = new List<OspoLink>();

        [JsonIgnore]
        public IReadOnlyDictionary<string, OspoLink> LinkByLogin { get; set; } = new Dictionary<string, OspoLink>();

        public static string GetCacheLocation()
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachedDirectory = Path.Combine(localAppData, fileInfo.CompanyName, fileInfo.ProductName, "Cache");
            return Path.Combine(cachedDirectory, $"ms-links.json");
        }

        public static Task<OspoLinkSet> LoadFromCacheAsync(string cacheLocation = null)
        {
            var path = string.IsNullOrEmpty(cacheLocation)
                        ? GetCacheLocation()
                        : cacheLocation;

            if (!File.Exists(path))
                return null;

            return LoadAsync(path);
        }

        public static async Task<OspoLinkSet> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
                return await LoadAsync(stream);
        }

        public static async Task<OspoLinkSet> LoadAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var linkSet = await JsonSerializer.DeserializeAsync<OspoLinkSet>(stream, options);
            linkSet.Initialize();
            return linkSet;
        }

        public async Task SaveAsync(string path)
        {
            var cacheDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(cacheDirectory);

            using (var stream = File.Create(path))
                await SaveAsync(stream);
        }

        public async Task SaveAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            await JsonSerializer.SerializeAsync(stream, this, options);
        }
    }
}
