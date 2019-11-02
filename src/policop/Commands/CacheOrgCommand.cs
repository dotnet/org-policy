using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheOrgCommand : ToolCommand
    {
        private string _orgName;
        private string _token;

        public override string Name => "cache-org";

        public override string Description => "Downloads the organization data from GitHub";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("token=", "The GitHub {token} to be used.", v => _token = v);
        }

        public override async Task ExecuteAsync()
        {
            var path = CachedOrg.GetCacheLocation(_orgName);
            var client = await GitHubClientFactory.CreateAsync(_token);
            await CachedOrg.LoadAsync(client, _orgName, cacheLocation: path, forceUpdate: true);
        }
    }
}
