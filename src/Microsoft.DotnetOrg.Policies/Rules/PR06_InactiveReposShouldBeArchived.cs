using System;
using System.Collections.Generic;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR06_InactiveReposShouldBeArchived : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            var now = DateTimeOffset.Now;
            var threshold = TimeSpan.FromDays(365);

            foreach (var repo in context.Org.Repos)
            {
                var inactivity = repo.LastPush - now;
                if (inactivity > threshold)
                {
                    yield return new PolicyViolation(
                        "PR06",
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
}
