using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftTeamShouldOnlyGrantReadAccess : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftTeamShouldOnlyGrantReadAccess;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            var microsoftTeam = org.GetMicrosoftTeam();
            if (microsoftTeam == null)
                yield break;

            foreach (var repo in org.Repos)
            {
                foreach (var teamAccess in repo.Teams)
                {
                    if (teamAccess.Team == microsoftTeam &&
                        teamAccess.Permission != CachedPermission.Pull)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            $"Repo '{repo.Name}' shouldn't grant '{teamAccess.Team.Name}' '{teamAccess.Permission.ToString().ToLower()}' permissions.",
                            repo: repo,
                            team: teamAccess.Team
                        );
                    }
                }
            }
        }
    }
}
