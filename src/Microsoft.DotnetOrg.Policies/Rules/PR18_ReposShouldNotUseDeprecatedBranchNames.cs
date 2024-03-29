﻿namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR18_ReposShouldNotUseDeprecatedBranchNames : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR18",
        "Repos shouldn't use deprecated branch names",
        PolicySeverity.Warning
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        var deprecatedBranchNames = new (string Deprecated, string Preferred)[]
        {
            ("master", "main")
        };

        foreach (var repo in context.Org.Repos)
        {
            if (repo.IsArchivedOrSoftArchived())
                continue;

            if (repo.IsFork)
                continue;

            if (!repo.IsOwnedByMicrosoft())
                continue;

            foreach (var branch in repo.Branches)
            {
                foreach (var (deprecatedName, preferredName) in deprecatedBranchNames)
                {
                    if (branch.Name.Contains(deprecatedName, StringComparison.OrdinalIgnoreCase))
                    {
                        context.ReportViolation(
                            Descriptor,
                            $"Repo '{repo.Name}' uses deprecated branch name '{branch.Name}'",
                            $@"
                                    The repo {repo.Markdown()} contains the branch {branch.Markdown()} which contains the deprecated branch name '{deprecatedName}'. It should use the name '{preferredName}'.
                                ",
                            repo: repo,
                            branch: branch
                        );
                    }
                }
            }
        }
    }
}