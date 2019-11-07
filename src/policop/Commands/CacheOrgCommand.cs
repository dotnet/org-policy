using System;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheOrgCommand : ToolCommand
    {
        private string _orgName;
        private string _gitHubToken;
        private string _ospoToken;
        private bool _includeLinks;

        public override string Name => "cache-org";

        public override string Description => "Downloads the organization data from GitHub";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("with-ms-links", "Include linking information to Microsoft users", v => _includeLinks = true)
                   .Add("github-token=", "The GitHub {token} to be used.", v => _gitHubToken = v)
                   .Add("ospo-token=", "The Microsoft Open Source Program Office {token} to be used.", v => _ospoToken = v);
        }

        public override async Task ExecuteAsync()
        {
            var githubClient = await GitHubClientFactory.CreateAsync(_gitHubToken);
            var ospoClient = !_includeLinks ? null : await OspoClientFactory.CreateAsync(_ospoToken);
            var result = await CachedOrg.LoadAsync(githubClient, _orgName, Console.Out, ospoClient);
            await CacheManager.StoreOrgAsync(result);
        }
    }
}
