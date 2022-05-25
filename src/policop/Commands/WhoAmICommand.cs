using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class WhoAmICommand : ToolCommand
    {
        public override string Name => "whoami";

        public override string Description => "Displays the GitHub user account being used";

        public override void AddOptions(OptionSet options)
        {
        }

        public override async Task ExecuteAsync()
        {
            var client = await GitHubClientFactory.CreateAsync();
            var me = await client.User.Current();
            Console.WriteLine(me.Login);
        }
    }
}
