using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var repo in org.Repos)
            {
                var isRepoOwnedByMicrosoft = repo.IsOwnedByMicrosoft();
                if (isRepoOwnedByMicrosoft)
                {
                    foreach (var userAccess in repo.Users)
                    {
                        var userWorksForMicrosoft = userAccess.User.IsInMicrosoftTeam() ||
                                                    userAccess.User.IsClaimingToBeWorkingForMicrosoft();
                        var isKnownBot = userAccess.User.IsKnownBot();
                        if (!userWorksForMicrosoft && !isKnownBot && userAccess.Permission != CachedPermission.Pull)
                        {
                            yield return new PolicyViolation(
                                Descriptor,
                                $"Non-Microsoft contributor '{userAccess.User.Login}' was granted more than 'pull' permissions to Microsoft-owned repo '{repo.Name}'.",
                                repo: repo,
                                user: userAccess.User
                            );
                        }
                    }
                }
            };
        }
    }
}
