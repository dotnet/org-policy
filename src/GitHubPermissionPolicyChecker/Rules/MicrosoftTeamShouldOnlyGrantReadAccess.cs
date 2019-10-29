using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftTeamShouldOnlyGrantReadAccess : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftTeamShouldOnlyGrantReadAccess;

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
                            $"Repo '{repo.Name}' should only grant '{teamAccess.Team.Name}' with 'read' permissions (but did grant '{teamAccess.Permission.ToString().ToLower()}').",
                            repo: repo,
                            team: teamAccess.Team
                        );
                    }
                }
            }
        }
    }
}
