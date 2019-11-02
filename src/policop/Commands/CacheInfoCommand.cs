using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheInfoCommand : ToolCommand
    {
        public override string Name => "cache-info";

        public override string Description => "Displays information about the cached data";

        public override void AddOptions(OptionSet options)
        {
        }

        public override Task ExecuteAsync()
        {
            Console.WriteLine();

            var ospoPath = OspoLinkSet.GetCacheLocation();
            var ospoInfo = new FileInfo(ospoPath);
            var ospoState = ospoInfo.Exists ? ospoInfo.LastWriteTime.ToString() : "missing";

            Console.WriteLine("Microsoft link data from the Open Source Program Office (OSPO)");
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine();

            if (!ospoInfo.Exists)
            {
                Console.WriteLine($"    missing");
            }
            else
            {
                Console.WriteLine($"    {ospoPath}");
                Console.WriteLine($"    {ospoState}");
            }

            Console.WriteLine();
            Console.WriteLine("Organization data from GitHub");
            Console.WriteLine("-----------------------------");
            Console.WriteLine();

            var orgDirectory = Path.GetDirectoryName(CachedOrg.GetCacheLocation("dummy"));
            var cachedOrgPaths = Directory.EnumerateFiles(orgDirectory, "*.json")
                                           .Where(f => f != ospoPath)
                                           .ToArray();

            if (cachedOrgPaths.Length == 0)
            {
                Console.WriteLine($"    missing");
            }
            else
            {
                foreach (var cachedOrgPath in cachedOrgPaths)
                {
                    var cachedOrgInfo = new FileInfo(cachedOrgPath);
                    Console.WriteLine($"    {cachedOrgPath}");
                    Console.WriteLine($"    {cachedOrgInfo.LastWriteTime}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine();

            return Task.CompletedTask;
        }
    }
}
