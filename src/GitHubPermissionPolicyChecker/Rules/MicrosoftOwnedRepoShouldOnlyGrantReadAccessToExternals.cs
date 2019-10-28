using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var repo in org.Repos)
            {
                var isOwnedByMicrosoft = repo.IsOwnedByMicrosoft();
                if (isOwnedByMicrosoft)
                {
                    foreach (var userAccess in repo.Users)
                    {
                        var isEmployee = org.IsEmployee(userAccess.User);
                        var isKnownBot = org.IsKnownBot(userAccess.User);
                        if (!isEmployee && !isKnownBot && userAccess.Permission != CachedPermission.Pull)
                        {
                            yield return new PolicyViolation(
                                $"External contributor '{userAccess.User}' was granted more than 'pull' permissions to repo '{repo.Name}'.",
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
