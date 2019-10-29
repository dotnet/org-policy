using System.Collections.Generic;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftOwnedTeamShouldOnlyContainEmployees : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftOwnedTeamShouldOnlyContainEmployees;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var team in context.Org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                if (isOwnedByMicrosoft)
                {
                    foreach (var user in team.Members)
                    {
                        var isMicrosoftUser = context.IsMicrosoftUser(user);
                        if (!isMicrosoftUser)
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
