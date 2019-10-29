using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class UnusedTeamShouldNotExist : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.UnusedTeamShouldBeRemoved;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var team in org.Teams)
            {
                var hasChildren = team.Children.Any();
                var hasRepos = team.Repos.Any();
                var isUsed = hasChildren || hasRepos;

                if (!isUsed)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"Team '{team.Name}' doesn't have any associated repos nor nested teams. It should either be used or removed.",
                        team: team
                    );
                }
            }
        }
    }

}
