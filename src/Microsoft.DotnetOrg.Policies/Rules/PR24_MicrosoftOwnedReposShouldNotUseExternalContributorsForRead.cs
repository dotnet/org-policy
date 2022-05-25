using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR24_MicrosoftOwnedReposShouldNotUseExternalContributorsForRead : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR24",
        "Microsoft-owned repos should not give read access to external contributors",
        PolicySeverity.Warning
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        var team = context.Org.Teams.SingleOrDefault(t => string.Equals(t.Name, "external-ci-access"));
        if (team is null)
            return;

        foreach (var repo in context.Org.Repos)
        {
            if (!repo.IsOwnedByMicrosoft())
                continue;

            var collaborators = repo.Users.Where(u => !u.User.IsBot() &&
                                                      u.Permission == CachedPermission.Read &&
                                                      u.Describe().IsCollaborator);
            foreach (var collaborator in collaborators)
            {
                context.ReportViolation(
                    Descriptor,
                    $"Repo '{repo.Name}' should not give explicit read access to user '{collaborator.User.Login}'",
                    $@"
                        For read access, add the user {collaborator.User.Markdown()} to {team.Markdown()} and grant that team read access to {repo.Markdown()}.
                        ",
                    repo: repo,
                    user: collaborator.User
                );
            }
        }
    }
}