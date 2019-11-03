using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.DevOps;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheBuildCommand : ToolCommand
    {
        private bool _refreshOrg;
        private bool _refreshLinks;
        private string _token;

        public override string Name => "cache-build";

        public override string Description => "Caches the org and link data from the latest build";

        public override void AddOptions(OptionSet options)
        {
            options.Add("org", "Refresh org data", v => _refreshOrg = true)
                   .Add("links", "Refresh link data", v => _refreshLinks = true)
                   .Add("token=", "The Azure DevOps API {token} to be used.", v => _token = v);

        }

        public override Task ExecuteAsync()
        {
            var files = new List<(string RemoteFileName, string LocalFileName)>();

            var includeAll = !_refreshOrg && !_refreshLinks;

            if (_refreshOrg || includeAll)
                files.Add(("dotnet.json", CacheManager.GetOrgCache("dotnet").FullName));

            if (_refreshLinks || includeAll)
                files.Add(("ms-links.json", CacheManager.GetLinkCache().FullName));

            try
            {
                return DownloadFilesAsync(_token, files);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        public static async Task DownloadFilesAsync(string token, IEnumerable<(string RemoteFileName, string LocalFileName)> files)
        {
            var client = await DevOpsClientFactory.CreateAsync("terrajobst", "github-policy-checker", token);

            var builds = await client.GetBuildsAsync("19", resultFilter: "succeeded", reasonFilter: "schedule,manual");
            var latestBuild = builds.FirstOrDefault();

            if (latestBuild == null)
            {
                Console.Error.WriteLine($"warn: can't find any suitable build on Azure DevOps");
                return;
            }

            foreach (var (remoteFileName, localFileName) in files)
            {
                try
                {
                    using (var remoteStream = await client.GetArtifactFileAsync(latestBuild.Id, "drop", remoteFileName))
                    {
                        var directory = Path.GetDirectoryName(localFileName);
                        Directory.CreateDirectory(directory);

                        using (var localStream = File.Create(localFileName))
                            await remoteStream.CopyToAsync(localStream);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: can't download {remoteFileName}: {ex.Message}");
                }
            }
        }
    }
}
