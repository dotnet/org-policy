using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ChangeVisibilityCommand : ToolCommand
    {
        private string? _orgName;
        private string? _repoName;
        private bool _makePrivate;
        private bool _makePublic;

        public override string Name => "change-visibility";

        public override string Description => "Makes a repo public or private";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r=", "Specifies the {name} of the repo", v => _repoName = v)
                   .Add("private", "Makes the repo private", v => _makePrivate = true)
                   .Add("public", "Makes the repo public", v => _makePublic = true);
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (string.IsNullOrEmpty(_repoName))
            {
                Console.Error.WriteLine($"error: -r must be specified");
                return;
            }

            if (!(_makePrivate ^ _makePublic))
            {
                Console.Error.WriteLine($"error: must specify either --private or --public");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();

            var update = new RepositoryUpdate(_repoName);
            update.Private = _makePrivate;

            try
            {
                await client.Repository.Edit(_orgName, _repoName, update);
            }
            catch (ApiException ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }
    }
}
