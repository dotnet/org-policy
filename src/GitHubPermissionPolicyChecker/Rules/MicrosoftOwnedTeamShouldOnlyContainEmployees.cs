using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedTeamShouldOnlyContainEmployees : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var team in org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                if (isOwnedByMicrosoft)
                {
                    foreach (var user in team.Members)
                    {
                        var isEmployee = org.IsEmployee(user);
                        if (!isEmployee)
                        {
                            yield return new PolicyViolation(
                                $"Microsoft owned team '{user}' shouldn't contain '{team.Name}' because they are not an employee.",
                                team: team,
                                user: user
                            );
                        }
                    }
                }
            };
        }
    }
}
