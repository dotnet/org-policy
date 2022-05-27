using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR02_MicrosoftOwnedRepoShouldAtMostGrantTriageToExternals : PolicyRule
{
    private const CachedPermission _maxPermission = CachedPermission.Triage;

    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR02",
        $"Microsoft-owned repo should at most grant '{_maxPermission.ToString().ToLower()}' to externals",
        PolicySeverity.Error
    );

    public override void GetViolations(PolicyAnalysisContext context)
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
                    if (!userWorksForMicrosoft && permission > _maxPermission)
                    {
                        context.ReportViolation(
                            Descriptor,
                            title: $"Non-Microsoft contributor '{user.Login}' should at most have '{_maxPermission.ToString().ToLower()}' permission for '{repo.Name}'",
                            body: $@"
                                    The non-Microsoft contributor {user.Markdown()} was granted {permission.Markdown()} for the Microsoft-owned repo {repo.Markdown()}.

                                    Only Microsoft users should have more than {_maxPermission.Markdown()} permissions.

                                    * If this is a Microsoft user, they need to [link](https://docs.opensource.microsoft.com/tools/github/accounts/linking.html) their account.
                                    * If this isn't a Microsoft user, their permission needs to be changed to `triage`.
                                ",
                            repo: repo,
                            user: user
                        );
                    }
                }
            }
        };
    }
}