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
                                title: $"Microsoft owned team '{team.Name}' shouldn't contain '{user.Login}'",
                                body: $@"
                                    Microsoft owned team {team.Markdown()} shouldn't contain user {user.Markdown()} because they are not an employee.

                                    * If this is a Microsoft user, they need to [link](https://docs.opensource.microsoft.com/tools/github/accounts/linking.html) their account.
                                    * If this isn't a Microsoft user, their permission needs to be changed to `pull`.
                                ",
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
