using System.Collections.Generic;
using System.Linq;
using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var team in org.Teams)
            {
                if (!team.IsOwnedByMicrosoft())
                {
                    var numberOfEmployees = team.Members.Count(m => org.IsEmployee(m));
                    var shouldBeMicrosoft = numberOfEmployees == team.Members.Count() ||
                                            team.Members.Count > 2 && numberOfEmployees > team.Members.Count / 2;

                    if (shouldBeMicrosoft)
                    {
                        yield return new PolicyViolation(
                            $"Team '{team.Name}' has mostly Microsoft employees on it. It should probbably be owned by Microsoft.",
                            team: team
                        );
                    }
                }
            }
        }
    }
}
