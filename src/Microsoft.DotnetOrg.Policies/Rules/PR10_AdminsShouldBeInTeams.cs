using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR10_AdminsShouldBeInTeams : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR10",
        "Admins should be in teams",
        PolicySeverity.Error
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        foreach (var repo in context.Org.Repos)
        {
            if (repo.IsArchived || !repo.IsOwnedByMicrosoft())
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

                if (isAdmin)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Admin access for '{user.Login}' in repo '{repo.Name}' should be granted via a team",
                        $@"
                                The user {user.Markdown()} shouldn't be directly added as an admin for repo {repo.Markdown()}. Instead, the user should be in a team that is granted `admin` permissions.

                                {recommendation}
                            ",
                        repo: repo,
                        user: user
                    );
                }
            }
        }
    }
}