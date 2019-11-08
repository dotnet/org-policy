using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR09_TooManyTeamMaintainers : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR09",
            "Too many team maintainers",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 4;

            foreach (var team in context.Org.Teams)
            {
                var numberOfMaintainers = team.Maintainers.Count(m => !m.IsOwner);

                if (numberOfMaintainers > Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Team '{team.Name}' has too many maintainers",
                        body: $@"
                            The team {team.Markdown()} has {numberOfMaintainers} maintainers. Reduce the number of maintainers to {Threshold} or less.
                        ",
                        org: context.Org,
                        team: team
                    );
                }
            }
        }
    }
}
