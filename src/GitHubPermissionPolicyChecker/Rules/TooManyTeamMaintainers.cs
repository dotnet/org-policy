using System.Collections.Generic;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class TooManyTeamMaintainers : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.TooManyTeamMaintainers;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 4;

            foreach (var team in context.Org.Teams)
            {
                var numberOfMaintainers = team.Maintainers.Count;

                if (numberOfMaintainers > Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"The team '{team.Name}' has {numberOfMaintainers} maintainers. Reduce the number of maintainers to {Threshold} or less.",
                        team: team
                    );
                }
            }
        }
    }
}
