using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var team in org.Teams)
            {
                if (!team.IsOwnedByMicrosoft())
                {
                    var numberOfEmployees = team.Members.Count(m => m.IsInMicrosoftTeam());
                    var numberOfMembers = team.Members.Count;
                    var shouldBeMicrosoft = numberOfEmployees == numberOfMembers ||
                                            numberOfMembers > 2 && numberOfEmployees > numberOfMembers / 2;

                    if (shouldBeMicrosoft)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            $"Team '{team.Name}' has mostly Microsoft employees on it. It should probbably be owned by Microsoft.",
                            team: team
                        );
                    }
                }
            }
        }
    }
}
