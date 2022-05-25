namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR19_DefaultBranchesShouldBeProtected : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR19",
            "Default branches should have branch protection",
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

                // Let's ignore uninitialized repos
                if (repo.DefaultBranch is null)
                    continue;

                if (!repo.DefaultBranch.Rules.Any())
                {
                    context.ReportViolation(
                        Descriptor,
                        $"The default branch '{repo.DefaultBranch.Name}' in '{repo.Name}' has no branch protection",
                        $@"
                            The default branch {repo.DefaultBranch.Markdown()} in repo {repo.Markdown()} should have branch protection rules, such as preventing force pushes and requiring PRs.
                        ",
                        repo: repo,
                        branch: repo.DefaultBranch
                    );
                }
            }
        }
    }
}
