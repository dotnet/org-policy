using System;
using System.Collections.Generic;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class InactiveReposShouldBeArchived : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.InactiveReposShouldBeArchived;

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
                        Descriptor,
                        $"The last push to repo '{repo.Name}' is more than {threshold.TotalDays:N0} days ago. It should be archived.",
                        repo: repo
                    );
                }
            }
        }
    }
}
