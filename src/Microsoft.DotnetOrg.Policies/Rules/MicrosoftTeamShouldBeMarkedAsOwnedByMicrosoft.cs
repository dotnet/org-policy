using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            var allowedPermission = CachedPermission.Pull;
            var microsoftTeam = context.Org.GetMicrosoftTeam();

            foreach (var team in context.Org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                var grantsMoreThanPullAccessToMicrosoftRepo = team.Repos.Any(r => r.Permission != allowedPermission && r.Repo.IsOwnedByMicrosoft()); ;

                if (!isOwnedByMicrosoft && grantsMoreThanPullAccessToMicrosoftRepo)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Team '{team.Name}' must be owned by Microsoft",
                        body: $@"
                            Team {team.Markdown()} grants at least one Microsoft-owned repo more than {allowedPermission.Markdown()} permissions. The team must be owned by Microsoft.

                            To indicate that the team is owned by Microsoft, ensure that one of the parent teams is {microsoftTeam.Markdown()}.
                        ",
                        team: team
                    );
                }
            }
        }
    }
}
