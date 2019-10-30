using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR07_UnusedTeamShouldNotExist : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var team in context.Org.Teams)
            {
                var hasChildren = team.Children.Any();
                var hasRepos = team.Repos.Any();
                var isUsed = hasChildren || hasRepos;

                if (!isUsed)
                {
                    yield return new PolicyViolation(
                        "PR07",
                        title: $"Unused team '{team.Name}' should be removed",
                        body: $@"
                            Team {team.Markdown()} doesn't have any associated repos nor nested teams. It should either be used or removed.
                        ",
                        team: team
                    );
                }
            }
        }
    }

}
