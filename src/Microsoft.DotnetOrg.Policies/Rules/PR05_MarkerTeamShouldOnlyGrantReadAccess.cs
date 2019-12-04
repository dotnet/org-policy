using System.Collections.Generic;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR05_MarkerTeamShouldOnlyGrantReadAccess : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR05",
            "Marker team should only grant 'read' access",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                foreach (var teamAccess in repo.Teams)
                {
                    var team = teamAccess.Team;
                    if (team.IsMarkerTeam() &&
                        teamAccess.Permission != CachedPermission.Read)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            title: $"Repo '{repo.Name}' should only grant '{team.Name}' with 'read' permissions",
                            body: $@"
                                The marker team {team.Markdown()} is only used to indicate ownership. It should only ever grant `read` permissions.

                                Change the permissions for {team.Markdown()} in repo {repo.Markdown()} to `read`.
                            ",
                            org: context.Org,
                            repo: repo,
                            team: team
                        );
                    }
                }
            }
        }
    }
}
