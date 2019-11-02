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
    internal sealed class CacheExportCommand : ToolCommand
    {
        private string _outputDirectory;

        public override string Name => "cache-export";

        public override string Description => "Exports the cached data to a directory";

        public override void AddOptions(OptionSet options)
        {
            options.Add("o=", "The {path} to the directory where the cache data should be written to.", v => _outputDirectory = v);
        }

        public override Task ExecuteAsync()
        {
            if (_outputDirectory == null)
            {
                Console.Error.WriteLine($"error: -o must be specified");
                return Task.CompletedTask;
            }

            var ospoPath = OspoLinkSet.GetCacheLocation();
            var orgDirectory = Path.GetDirectoryName(CachedOrg.GetCacheLocation("dummy"));
            var cachedOrgPaths = Directory.EnumerateFiles(orgDirectory, "*.json")
                                           .Where(f => f != ospoPath)
                                           .ToArray();

            var files = new List<string>();
            files.Add(ospoPath);
            files.AddRange(cachedOrgPaths);

            _outputDirectory = Path.GetFullPath(_outputDirectory);

            foreach (var sourcePath in files)
            {
                var name = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(_outputDirectory, name);

                if (File.Exists(sourcePath))
                {
                    var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
                    Directory.CreateDirectory(destinationDirectoryPath);
                }
             
                File.Copy(sourcePath, destinationPath, true);
            }

            return Task.CompletedTask;
        }
    }

}
