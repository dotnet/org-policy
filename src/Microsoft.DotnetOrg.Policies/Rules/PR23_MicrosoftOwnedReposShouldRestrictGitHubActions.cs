using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR23_MicrosoftOwnedReposShouldRestrictGitHubActions : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR23",
            "Microsoft-owned repos should restrict GitHub Actions",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                if (!repo.IsOwnedByMicrosoft())
                    continue;

                if (!repo.Workflows.Any())
                    continue;

                if (repo.ActionPermissions.AllowedActions == CachedRepoAllowedActions.Disabled ||
                    repo.ActionPermissions.AllowedActions == CachedRepoAllowedActions.LocalOnly ||
                    repo.ActionPermissions.AllowedActions == CachedRepoAllowedActions.Selected)
                    continue;

                context.ReportViolation(
                    Descriptor,
                    $"Repo '{repo.Name}' should restrict GitHub Actions",
                    $@"
                        The repo {repo.Markdown()} shouldn't allow all GitHub Actions but restrict which actions can be used by either selecting **Local Only** or by [specifying a list of patterns](https://docs.github.com/en/github/administering-a-repository/managing-repository-settings/disabling-or-limiting-github-actions-for-a-repository#allowing-specific-actions-to-run) that describe which actions are allowed.
                    ",
                    repo: repo
                );
            }
        }
    }

}
