using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftEmployeesShouldBeInMicrosoftTeam : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftEmployeesShouldBeInMicrosoftTeam;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            var microsofTeam = org.GetMicrosoftTeam();
            if (microsofTeam == null)
                yield break;

            foreach (var user in org.Users)
            {
                var userClaimsToBeWorkingForMicrosoft = user.IsClaimingToBeWorkingForMicrosoft();
                var userIsInMicrosoftTeam = user.IsInMicrosoftTeam();

                if (userClaimsToBeWorkingForMicrosoft && !userIsInMicrosoftTeam)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"User '{user.Login}' seems to be a Microsoft employee. They should be added to the team '{microsofTeam.Name}'.",
                        user: user,
                        team: microsofTeam,
                        receivers: new[] { user }
                    );
                }
            }
        }
    }
}
