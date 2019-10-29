using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedTeamShouldOnlyContainEmployees : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftOwnedTeamShouldOnlyContainEmployees;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            foreach (var team in org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                if (isOwnedByMicrosoft)
                {
                    foreach (var user in team.Members)
                    {
                        var isEmployee = user.IsInMicrosoftTeam();
                        if (!isEmployee)
                        {
                            yield return new PolicyViolation(
                                Descriptor,
                                $"Microsoft owned team '{user.Login}' shouldn't contain '{team.Name}' because they are not an employee.",
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
