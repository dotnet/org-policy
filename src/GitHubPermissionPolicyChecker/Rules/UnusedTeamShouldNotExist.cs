using System.Collections.Generic;
using System.Linq;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class UnusedTeamShouldNotExist : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.UnusedTeamShouldBeRemoved;

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
                        Descriptor,
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
