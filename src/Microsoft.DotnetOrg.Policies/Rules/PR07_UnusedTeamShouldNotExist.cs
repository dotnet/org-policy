using System.Collections.Generic;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR07_UnusedTeamShouldNotExist : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR07",
            "Unused team should be removed",
            PolicySeverity.Warning
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var team in context.Org.Teams)
            {
                if (team.IsUnused())
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Unused team '{team.Name}' should be removed",
                        body: $@"
                            Team {team.Markdown()} doesn't have any associated repos nor nested teams. It should either be used or removed.
                        ",
                        org: context.Org,
                        team: team
                    );
                }
            }
        }
    }
}
