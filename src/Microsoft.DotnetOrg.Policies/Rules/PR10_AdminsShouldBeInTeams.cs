using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR10_AdminsShouldBeInTeams : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR10",
            "Admins should be in teams",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                if (!repo.IsOwnedByMicrosoft())
                    continue;

                var adminTeams = repo.Teams.Where(ta => ta.Permission == CachedPermission.Admin)
                                           .Select(ta => ta.Team);

                var recommendation = string.Join(", ", adminTeams.Select(a => a.Markdown()));

                if (recommendation.Length > 0)
                    recommendation = "Consider adding the user to one of these teams: " + recommendation + ".";
                else
                    recommendation = "Grant a team `admin` permissions for this repo and ensure the user is in that team.";

                foreach (var userAccess in repo.Users)
                {
                    var user = userAccess.User;
                    var isAdmin = userAccess.Permission == CachedPermission.Admin;
                    var isDirectlyAssigned = userAccess.Describe().IsCollaborator;

                    if (isAdmin && isDirectlyAssigned)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            title: $"Admin access for '{user.Login}' in repo '{repo.Name}' should be granted via a team",
                            body: $@"
                                The user {user.Markdown()} shouldn't be directly added as an admin for repo {repo.Markdown()}. Instead, the user should be in a team that is granted `admin` permissions.

                                {recommendation}
                            ",
                            org: context.Org,
                            repo: repo,
                            user: user
                        );
                    }
                }
            }
        }
    }
}
