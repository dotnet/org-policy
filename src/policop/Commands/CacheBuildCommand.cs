using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.DevOps;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheBuildCommand : ToolCommand
    {
        private string _token;

        public override string Name => "cache-build";

        public override string Description => "Caches the org from the latest build";

        public override void AddOptions(OptionSet options)
        {
            options.Add("token=", "The Azure DevOps API {token} to be used.", v => _token = v);
        }

        public override Task ExecuteAsync()
        {
            var orgName = "dotnet";
            var remoteFileName = orgName + ".json";
            var localFileName = CacheManager.GetOrgCache(orgName).FullName;
            return DownloadFileAsync(_token, remoteFileName, localFileName);
        }

        public static async Task DownloadFileAsync(string token, string remoteFileName, string localFileName)
        {
            var client = await DevOpsClientFactory.CreateAsync("dnceng", "internal", token);

            var builds = await client.GetBuildsAsync("653", resultFilter: "succeeded", reasonFilter: "schedule,manual");
            var latestBuild = builds.FirstOrDefault();

            if (latestBuild == null)
            {
                Console.Error.WriteLine($"warn: can't find any suitable build on Azure DevOps");
                return;
            }

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
