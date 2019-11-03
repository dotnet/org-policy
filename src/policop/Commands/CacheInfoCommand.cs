using System;
using System.Linq;
using System.Threading.Tasks;

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

            var linkCache = CacheManager.GetLinkCache();

            Console.WriteLine("Microsoft link data from the Open Source Program Office (OSPO)");
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine();

            if (!linkCache.Exists)
            {
                Console.WriteLine($"    missing");
            }
            else
            {
                Console.WriteLine($"    {linkCache.FullName}");
                Console.WriteLine($"    {linkCache.LastWriteTime.ToString()}");
            }

            Console.WriteLine();
            Console.WriteLine("Organization data from GitHub");
            Console.WriteLine("-----------------------------");
            Console.WriteLine();

            var orgCaches = CacheManager.GetOrgCaches().ToArray();

            if (orgCaches.Length == 0)
            {
                Console.WriteLine($"    missing");
            }
            else
            {
                foreach (var orgCache in orgCaches)
                {
                    Console.WriteLine($"    {orgCache.FullName}");
                    Console.WriteLine($"    {orgCache.LastWriteTime}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine();

            return Task.CompletedTask;
        }
    }
}
