using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            var allowedPermission = CachedPermission.Pull;

            foreach (var team in context.Org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                var grantsWriteAccessToMicrosoftRepo = team.Repos.Any(r => r.Permission != allowedPermission && r.Repo.IsOwnedByMicrosoft()); ;

                if (!isOwnedByMicrosoft && grantsWriteAccessToMicrosoftRepo)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"Team '{team.Name}' grants at least one Microsoft-owned repo more than '{allowedPermission.ToString().ToLower()}' permissions. The team must be owned by Microsoft.",
                        team: team
                    );
                }
            }
        }
    }
}
