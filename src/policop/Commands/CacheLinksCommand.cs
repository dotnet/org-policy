using System.Threading.Tasks;

using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CacheLinksCommand : ToolCommand
    {
        private string _token;

        public override string Name => "cache-links";

        public override string Description => "Downloads the Microsoft link data from the Open Source Program Office (OSPO)";

        public override void AddOptions(OptionSet options)
        {
            options.Add("token=", "The OSPO {token} to be used.", v => _token = v);
        }

        public override async Task ExecuteAsync()
        {
            var path = OspoLinkSet.GetCacheLocation();
            var client = await OspoClientFactory.CreateAsync(_token);
            var linkSet = await client.GetAllAsync();
            await linkSet.SaveAsync(path);
        }
    }
}
