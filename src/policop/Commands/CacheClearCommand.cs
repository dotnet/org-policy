using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheClearCommand : ToolCommand
    {
        private bool _clearOrg;
        private bool _clearLinks;
        private bool _force;

        public override string Name => "cache-clear";

        public override string Description => "Clears the cached org and link data";

        public override void AddOptions(OptionSet options)
        {
            options.Add("org", "Clears org data", v => _clearOrg = true)
                   .Add("links", "Clears link data", v => _clearLinks = true)
                   .Add("f", "Actually clears the cache", v => _force = true);
        }

        public override Task ExecuteAsync()
        {
            var includeAll = !_clearOrg && !_clearLinks;

            var files = new List<FileInfo>();

            if (_clearOrg || includeAll)
                files.AddRange(CacheManager.GetOrgCaches());

            if (_clearLinks || includeAll)
                files.Add(CacheManager.GetLinkCache());

            foreach (var file in files.Where(f => f.Exists))
            {
                Console.WriteLine($"rm: {file}");

                if (_force)
                    file.Delete();
            }

            if (!_force && files.Count > 0)
            {
                Console.WriteLine("info: no files deleted");
                Console.WriteLine("info: to actually delete files, specify -f");
            }

            return Task.CompletedTask;
        }
    }
}
