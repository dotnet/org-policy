﻿namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR06_InactiveReposShouldBeArchived : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR06",
        "Inactive repos should be archived",
        PolicySeverity.Warning
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        var now = DateTimeOffset.Now;
        var threshold = TimeSpan.FromDays(365);

        foreach (var repo in context.Org.Repos)
        {
            var alreadyArchived = repo.IsArchived;
            var inactivity = now - repo.LastPush;
            if (!alreadyArchived && inactivity > threshold)
            {
                context.ReportViolation(
                    Descriptor,
                    title: $"Inactive repo '{repo.Name}' should be archived",
                    body: $@"
                            The last push to repo {repo.Markdown()} is more than {threshold.TotalDays:N0} days ago. It should be archived.
                        ",
                    repo: repo
                );
            }
        }
    }
}