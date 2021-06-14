using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class BlockUserCommand : ToolCommand
    {
        private string? _orgName;
        private string? _userName;
        private bool _unblock;

        public override string Name => "block-user";

        public override string Description => "Blocks or unblocks a user across all our orgs";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("u=", "Specifies the user", v => _userName = v)
                   .Add("unblock", "Unblocks the user", v => _unblock = true);
        }

        public override async Task ExecuteAsync()
        {
            string[] orgs;

            if (_orgName is null || _orgName == "*")
                orgs = CacheManager.GetCachedOrgNames().ToArray();
            else
                orgs = new[] { _orgName };

            if (string.IsNullOrEmpty(_userName))
            {
                Console.Error.WriteLine($"error: -u must be specified");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();

            foreach (var org in orgs)
            {
                try
                {
                    if (_unblock)
                    {
                        await client.Connection.Delete(new Uri($"/orgs/{org}/blocks/{_userName}", UriKind.Relative));
                        Console.WriteLine($"Unblocked {_userName} in {org}.");
                    }
                    else
                    {
                        await client.Connection.Put(new Uri($"/orgs/{org}/blocks/{_userName}", UriKind.Relative));
                        Console.WriteLine($"Blocked {_userName} in {org}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: can't ban {_userName} in {org}: {ex.Message}");
                }
            }
        }
    }
}
