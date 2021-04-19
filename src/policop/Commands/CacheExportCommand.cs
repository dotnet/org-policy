using System;
using System.IO;
using System.Threading.Tasks;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheExportCommand : ToolCommand
    {
        private string? _outputDirectory;

        public override string Name => "cache-export";

        public override string Description => "Exports the cached orgs to a directory";

        public override void AddOptions(OptionSet options)
        {
            options.Add("o=", "The {path} to the directory where the cache data should be written to.", v => _outputDirectory = v);
        }

        public override Task ExecuteAsync()
        {
            if (_outputDirectory is null)
            {
                Console.Error.WriteLine($"error: -o must be specified");
                return Task.CompletedTask;
            }

            _outputDirectory = Path.GetFullPath(_outputDirectory);

            var orgCaches = CacheManager.GetOrgCaches();

            foreach (var orgCache in orgCaches)
            {
                var destinationPath = Path.Combine(_outputDirectory, orgCache.Name);

                if (orgCache.Exists)
                {
                    var destinationDirectoryPath = Path.GetDirectoryName(destinationPath)!;
                    Directory.CreateDirectory(destinationDirectoryPath);
                    orgCache.CopyTo(destinationPath, true);
                }
            }

            return Task.CompletedTask;
        }
    }
}
