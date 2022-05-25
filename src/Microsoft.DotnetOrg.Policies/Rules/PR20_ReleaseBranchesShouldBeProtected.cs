namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR20_ReleaseBranchesShouldBeProtected : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR20",
        "Release branches should have branch protection",
        PolicySeverity.Warning
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        foreach (var repo in context.Org.Repos)
        {
            if (repo.IsFork || repo.IsTemporaryForkForSecurityAdvisory())
                continue;

            if (repo.IsArchivedOrSoftArchived())
                continue;

            if (!repo.IsOwnedByMicrosoft())
                continue;

            var unprotectedReleaseBranches = repo.Branches.Where(b => b.Name.StartsWith("release", StringComparison.OrdinalIgnoreCase))
                .Where(b => !b.Rules.Any());

            foreach (var branch in unprotectedReleaseBranches)
            {
                context.ReportViolation(
                    Descriptor,
                    $"The release branch '{branch.Name}' in '{repo.Name}' has no branch protection",
                    $@"
                            The branch {branch.Markdown()} in repo {repo.Markdown()} appears to be a release branch and should have branch protection rules, such as preventing force pushes and requiring PRs.
                        ",
                    repo: repo,
                    branch: branch
                );
            }
        }
    }
}