using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal static class CacheManager
    {
        public static FileInfo GetLinkCache()
        {
            return new FileInfo(OspoLinkSet.GetCacheLocation());
        }

        public static FileInfo GetOrgCache(string orgName)
        {
            return new FileInfo(CachedOrg.GetCacheLocation(orgName));
        }

        public static IEnumerable<FileInfo> GetOrgCaches()
        {
            var orgCacheDirectory = Path.GetDirectoryName(CachedOrg.GetCacheLocation("dummy"));
            if (!Directory.Exists(orgCacheDirectory))
                return Array.Empty<FileInfo>();

            var cachedOrgs = Directory.EnumerateFiles(orgCacheDirectory, "*.json");
            return cachedOrgs.Where(o => o != GetLinkCache().FullName)
                             .Select(o => new FileInfo(o));
        }
    }
}
