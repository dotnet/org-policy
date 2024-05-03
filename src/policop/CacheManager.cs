using System.Diagnostics;
using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop;

internal static class CacheManager
{
    public static IEnumerable<string> GetCachedOrgNames()
    {
        return GetOrgCaches().Select(fi => Path.GetFileNameWithoutExtension(fi.Name));
    }

    public static string GetOrgCacheDirectory()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        var fileInfo = FileVersionInfo.GetVersionInfo(exePath)!;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var companyName = fileInfo.CompanyName ?? string.Empty;
        var productName = fileInfo.ProductName ?? string.Empty;
        var cachedDirectory = Path.Combine(localAppData, companyName, productName, "Cache");
        return cachedDirectory;
    }

    public static FileInfo GetOrgCache(string orgName)
    {
        var cachedDirectory = GetOrgCacheDirectory();
        var path = Path.Combine(cachedDirectory, $"{orgName}.json");
        return new FileInfo(path);
    }

    public static IEnumerable<FileInfo> GetOrgCaches()
    {
        var orgCacheDirectory = Path.GetDirectoryName(GetOrgCache("dummy").FullName)!;
        if (!Directory.Exists(orgCacheDirectory))
            return Array.Empty<FileInfo>();

        var cachedOrgs = Directory.EnumerateFiles(orgCacheDirectory, "*.json");
        return cachedOrgs.Select(o => new FileInfo(o));
    }

    public static async Task<CachedOrg?> LoadOrgAsync(string orgName)
    {
        var location = GetOrgCache(orgName);
        var cachedOrg = await CachedOrg.LoadAsync(location.FullName);

        var cacheIsValid = cachedOrg is not null &&
                           cachedOrg.Name == orgName &&
                           cachedOrg.Version == CachedOrg.CurrentVersion;

        return cacheIsValid ? cachedOrg : null;
    }

    public static Task StoreOrgAsync(CachedOrg result)
    {
        var location = GetOrgCache(result.Name);
        return result.SaveAsync(location.FullName);
    }
}