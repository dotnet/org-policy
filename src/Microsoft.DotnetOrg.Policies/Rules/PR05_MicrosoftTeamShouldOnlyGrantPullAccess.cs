using System.Collections.Generic;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR05_MicrosoftTeamShouldOnlyGrantPullAccess : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR05",
            "The team 'Microsoft' should only grant 'pull' access",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            var microsoftTeam = context.Org.GetMicrosoftTeam();
            if (microsoftTeam == null)
                yield break;

            foreach (var repo in context.Org.Repos)
            {
                foreach (var teamAccess in repo.Teams)
                {
                    if (teamAccess.Team == microsoftTeam &&
                        teamAccess.Permission != CachedPermission.Pull)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            title: $"Repo '{repo.Name}' should only grant '{teamAccess.Team.Name}' with 'pull' permissions",
                            body: $@"
                                The {microsoftTeam.Markdown()} is only used to indicate ownership. It should only ever grant `pull` permissions.

                                Change the permissions for {microsoftTeam.Markdown()} in repo {repo.Markdown()} to `pull`.
                            ",
                            org: context.Org,
                            repo: repo,
                            team: teamAccess.Team
                        );
                    }
                }
            }
        }
    }
}
