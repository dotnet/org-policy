using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheExportCommand : ToolCommand
    {
        private string _outputDirectory;

        public override string Name => "cache-export";

        public override string Description => "Exports the cached org and link data to a directory";

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

            var linkCache = CacheManager.GetLinkCache();
            var orgCaches = CacheManager.GetOrgCaches().ToArray();

            var files = new List<FileInfo>
            {
                linkCache
            };
            files.AddRange(orgCaches);

            _outputDirectory = Path.GetFullPath(_outputDirectory);

            foreach (var file in files)
            {
                var destinationPath = Path.Combine(_outputDirectory, file.Name);

                if (file.Exists)
                {
                    var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
                    Directory.CreateDirectory(destinationDirectoryPath);
                    file.CopyTo(destinationPath, true);
                }
            }

            return Task.CompletedTask;
        }
    }
}
