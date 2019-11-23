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
        private string _orgName;
        private string _buildId;

        public override string Name => "cache-build";

        public override string Description => "Caches the org from the latest build";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("build=", "The (optional) build {id} to use.", v => _buildId = v);
        }

        public override async Task ExecuteAsync()
        {
            var orgNames = !string.IsNullOrEmpty(_orgName)
                ? new[] { _orgName }
                : CacheManager.GetOrgCaches().Select(c => Path.GetFileNameWithoutExtension(c.Name)).ToArray();

            var client = await DevOpsClientFactory.CreateAsync("dnceng", "internal");
            var builds = await client.GetBuildsAsync("653", resultFilter: "succeeded", reasonFilter: "schedule,manual");
            var build = string.IsNullOrEmpty(_buildId)
                ? builds.FirstOrDefault()
                : builds.FirstOrDefault(b => b.Id.ToString() == _buildId);

            if (build == null)
            {
                if (_buildId == null)
                    Console.Error.WriteLine("error: no builds found");
                else
                    Console.Error.WriteLine($"error: can't find build {_buildId}");

                return;
            }

            foreach (var orgName in orgNames)
            {
                var remoteFileName = orgName + ".json";
                var localFileName = CacheManager.GetOrgCache(orgName).FullName;

                try
                {
                    await DownloadArtifactFileAsync(client, build, remoteFileName, localFileName);
                    Console.Error.WriteLine($"{orgName} -> {localFileName}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: can't download org data for {orgName}: {ex.Message}");
                }
            }
        }

        private static async Task DownloadArtifactFileAsync(DevOpsClient client, DevOpsBuild build, string remoteFileName, string localFileName)
        {
            using (var remoteStream = await client.GetArtifactFileAsync(build.Id, "drop", remoteFileName))
            {
                var directory = Path.GetDirectoryName(localFileName);
                Directory.CreateDirectory(directory);

                using (var localStream = File.Create(localFileName))
                    await remoteStream.CopyToAsync(localStream);
            }
        }
    }
}
