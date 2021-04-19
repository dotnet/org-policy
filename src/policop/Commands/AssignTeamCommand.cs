using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class AssignTeamCommand : ToolCommand
    {
        private string? _orgName;
        private string? _repoName;
        private string? _teamName;
        private string? _permission;
        private bool _unassign;

        public override string Name => "assign-team";

        public override string Description => "Assigns or unassigns a team";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r=", "Specifies the repo", v => _repoName = v)
                   .Add("t=", "Specifies the team", v => _teamName = v)
                   .Add("p=", "Sets the {permission} (default: read)", v => _permission = v)
                   .Add("d", "Unassigns the team", v => _unassign = true);
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

            if (string.IsNullOrEmpty(_teamName))
            {
                Console.Error.WriteLine($"error: -t must be specified");
                return;
            }

            Permission permission;

            switch (_permission)
            {
                case null:
                case "read":
                    permission = Permission.Pull;
                    break;
                case "write":
                    permission = Permission.Push;
                    break;
                case "admin":
                    permission = Permission.Admin;
                    break;
                default:
                    Console.Error.WriteLine($"error: permission can be 'read', 'write', or 'admin' but not '{_permission}'");
                    return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var teams = await client.Organization.Team.GetAll(_orgName);

            var team = teams.SingleOrDefault(t => string.Equals(t.Name, _teamName, StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(t.Slug, _teamName, StringComparison.OrdinalIgnoreCase));

            if (team is null)
            {
                Console.Error.WriteLine($"error: team '{_teamName}' doesn't exist");
                return;
            }

            Repository? repo;
            try
            {
                repo = await client.Repository.Get(_orgName, _repoName);
            }
            catch (Exception)
            {
                repo = null;
            }

            if (repo is null)
            {
                Console.Error.WriteLine($"error: repo '{_orgName}/{_repoName}' doesn't exist");
                return;
            }

            if (_unassign)
            {
                await client.Organization.Team.RemoveRepository(team.Id, _orgName, _repoName);
                Console.Out.WriteLine($"Removed team '{_teamName}' from repo '{_repoName}'");
            }
            else
            {
                await client.Organization.Team.AddRepository(team.Id, _orgName, _repoName, new RepositoryPermissionRequest(permission));
                Console.Out.WriteLine($"Added team '{_teamName}' to repo '{_repoName}'");
            }
        }
    }
}
