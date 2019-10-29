using System.Collections.Generic;
using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class TooManyTeamMaintainers : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.TooManyTeamMaintainers;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            const int Threshold = 4;

            foreach (var team in org.Teams)
            {
                var numberOfMaintainers = team.Maintainers.Count;

                if (numberOfMaintainers > Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"The team '{team.Name}' has more than {Threshold} maintainers ({numberOfMaintainers}). Reduce the number of maintainers.",
                        team: team
                    );
                }
            }
        }
    }
}
