using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheClearCommand : ToolCommand
    {
        private bool _clearOrg;
        private bool _clearLinks;
        private bool _force;

        public override string Name => "cache-clear";

        public override string Description => "Clears the cache the cache";

        public override void AddOptions(OptionSet options)
        {
            options.Add("org", "Clears org data", v => _clearOrg = true)
                   .Add("links", "Clears link data", v => _clearLinks = true)
                   .Add("f", "Actually clears the cache", v => _force = true);
        }

        public override Task ExecuteAsync()
        {
            var files = new List<string>();

            var includeAll = !_clearOrg && !_clearLinks;

            var ospoPath = OspoLinkSet.GetCacheLocation();
            var orgDirectory = Path.GetDirectoryName(CachedOrg.GetCacheLocation("dummy"));
            var orgFiles = Directory.EnumerateFiles(orgDirectory, "*.json").Where(f => f != ospoPath);

            if (_clearOrg || includeAll)
                files.AddRange(orgFiles);

            if (_clearLinks || includeAll)
                files.Add(ospoPath);

            files.RemoveAll(f => !File.Exists(f));

            foreach (var file in files)
            {
                Console.WriteLine($"rm: {file}");

                if (_force)
                    File.Delete(file);
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
