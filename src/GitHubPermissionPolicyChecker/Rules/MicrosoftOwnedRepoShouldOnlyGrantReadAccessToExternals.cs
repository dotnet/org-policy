using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                var isRepoOwnedByMicrosoft = repo.IsOwnedByMicrosoft();
                if (isRepoOwnedByMicrosoft)
                {
                    foreach (var userAccess in repo.Users.Where(ua => ua.Describe().IsCollaborator))
                    {
                        var userWorksForMicrosoft = context.IsMicrosoftUser(userAccess.User);
                        if (!userWorksForMicrosoft && userAccess.Permission != CachedPermission.Pull)
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
