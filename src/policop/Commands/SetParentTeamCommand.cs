using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class SetParentTeamCommand : ToolCommand
    {
        private string _orgName;
        private string _teamName;
        private string _parentTeam;
        private bool _unassign;

        public override string Name => "set-parent-team";

        public override string Description => "Sets the parent of a team";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("t=", "Specifies the team", v => _teamName = v)
                   .Add("p=", "Specifies the new team's parent", v => _parentTeam = v)
                   .Add("d", "Unassigns the parent", v => _unassign = true);
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (string.IsNullOrEmpty(_teamName))
            {
                Console.Error.WriteLine($"error: -t must be specified");
                return;
            }

            if (string.IsNullOrEmpty(_parentTeam) && !_unassign)
            {
                Console.Error.WriteLine($"error: either -p or -d must be specified");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var teams = await client.Organization.Team.GetAll(_orgName);

            Team FindTeam(string name)
            {
                var lastSlash = name.LastIndexOf('/');
                if (lastSlash >= 0)
                    name = name.Substring(lastSlash + 1);

                return teams.SingleOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t.Slug, name, StringComparison.OrdinalIgnoreCase));
            }

            var team = FindTeam(_teamName);
            var parentTeam = (Team) null;

            if (team == null)
            {
                Console.Error.WriteLine($"error: team '{_teamName}' doesn't exist");
                return;
            }

            if (_parentTeam != null)           
            {
                parentTeam = FindTeam(_parentTeam);
                if (parentTeam == null)
                {
                    Console.Error.WriteLine($"error: parent team '{_parentTeam}' doesn't exist");
                    return;
                }
            }

            var updateTeam = new UpdateTeam(team.Name);

            if (_unassign)
            {
                updateTeam.ParentTeamId = null;
                await client.Organization.Team.Update(team.Id, updateTeam);
                Console.Out.WriteLine($"Cleared parent of '{team.Slug}'");
            }
            else
            {
                updateTeam.ParentTeamId = parentTeam.Id;
                await client.Organization.Team.Update(team.Id, updateTeam);
                Console.Out.WriteLine($"Set parent of '{team.Slug}' to '{parentTeam.Slug}'");
            }
        }
    }
}
