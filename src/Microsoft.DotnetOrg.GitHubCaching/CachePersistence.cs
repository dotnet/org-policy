using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    internal static class CachePersistence
    {
        public static string GetPath(string orgName)
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachedDirectory = Path.Combine(localAppData, fileInfo.CompanyName, fileInfo.ProductName, "Cache");
            return Path.Combine(cachedDirectory, $"{orgName}.json");
        }

        public static async Task<CachedOrg> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                var orgData = await JsonSerializer.DeserializeAsync<CachedOrg>(stream, options);
                orgData.Initialize();
                return orgData;
            }
        }

        public static async Task SaveAsync(CachedOrg cachedOrg, string path)
        {
            var cacheDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(cacheDirectory);

            using (var stream = File.Create(path))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                await JsonSerializer.SerializeAsync(stream, cachedOrg, options);
            }
        }
    }
}
