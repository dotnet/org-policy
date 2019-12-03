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
        private bool _includeLinks;

        public override string Name => "cache-org";

        public override string Description => "Downloads the organization data from GitHub";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("with-ms-links", "Include linking information to Microsoft users", v => _includeLinks = true);
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            var connection = await GitHubClientFactory.CreateGraphAsync();
            var ospoClient = !_includeLinks ? null : await OspoClientFactory.CreateAsync();
            var result = await CachedOrg.LoadAsync(connection, _orgName, Console.Out, ospoClient);
            await CacheManager.StoreOrgAsync(result);
        }
    }
}
