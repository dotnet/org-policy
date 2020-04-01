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

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var team in context.Org.Teams)
            {
                if (team.IsUnused())
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Unused team '{team.Name}' should be removed",
                        $@"
                            Team {team.Markdown()} doesn't have any associated repos nor nested teams. It should either be used or removed.
                        ",
                        team: team
                    );
                }
            }
        }
    }
}
