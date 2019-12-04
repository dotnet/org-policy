using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR02_MicrosoftOwnedRepoShouldOnlyGrantPullAccessToExternals : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR02",
            "Microsoft-owned repo should only grant 'pull' to externals",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                var isRepoOwnedByMicrosoft = repo.IsOwnedByMicrosoft();
                if (isRepoOwnedByMicrosoft)
                {
                    foreach (var userAccess in repo.Users)
                    {
                        var user = userAccess.User;
                        var permission = userAccess.Permission;
                        var userWorksForMicrosoft = user.IsMicrosoftUser();
                        if (!userWorksForMicrosoft && permission != CachedPermission.Read)
                        {
                            yield return new PolicyViolation(
                                Descriptor,
                                title: $"Non-Microsoft contributor '{user.Login}' should only have 'pull' permission for '{repo.Name}'",
                                body: $@"
                                    The non-Microsoft contributor {user.Markdown()} was granted {permission.Markdown()} for the Microsoft-owned repo {repo.Markdown()}.

                                    Only Microsoft users should have more than `pull` permissions.

                                    * If this is a Microsoft user, they need to [link](https://docs.opensource.microsoft.com/tools/github/accounts/linking.html) their account.
                                    * If this isn't a Microsoft user, their permission needs to be changed to `pull`.
                                ",
                                org: context.Org,
                                repo: repo,
                                user: user
                            );
                        }
                    }
                }
            };
        }
    }
}
