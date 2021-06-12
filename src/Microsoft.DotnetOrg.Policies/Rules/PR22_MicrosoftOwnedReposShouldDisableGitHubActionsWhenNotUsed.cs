using Microsoft.DotnetOrg.GitHubCaching;

using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR22_MicrosoftOwnedReposShouldDisableGitHubActionsWhenNotUsed : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR22",
            "Microsoft-owned repo should disable GitHub Actions when it's not used",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                if (!repo.IsOwnedByMicrosoft())
                    continue;

                if (repo.Workflows.Any())
                    continue;

                if (repo.ActionPermissions.AllowedActions == CachedRepoAllowedActions.Disabled)
                    continue;

                context.ReportViolation(
                    Descriptor,
                    $"Repo '{repo.Name}' should should disable GitHub Actions",
                    $@"
                        The repo {repo.Markdown()} doesn't have any workflows and thus should disable GitHub actions.
                    ",
                    repo: repo
                );
            }
        }
    }
}
