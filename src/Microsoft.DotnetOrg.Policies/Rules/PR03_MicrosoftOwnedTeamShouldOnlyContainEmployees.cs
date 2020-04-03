namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR03_MicrosoftOwnedTeamShouldOnlyContainEmployees : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR03",
            "Microsoft-owned team should only contain Microsoft users",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var team in context.Org.Teams)
            {
                var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
                if (isOwnedByMicrosoft)
                {
                    foreach (var user in team.Members)
                    {
                        var isMicrosoftUser = user.IsMicrosoftUser();
                        if (!isMicrosoftUser)
                        {
                            context.ReportViolation(
                                Descriptor,
                                $"Microsoft owned team '{team.Name}' shouldn't contain '{user.Login}'",
                                $@"
                                    Microsoft owned team {team.Markdown()} shouldn't contain user {user.Markdown()} because they are not an employee.

                                    * If this is a Microsoft user, they need to [link](https://docs.opensource.microsoft.com/tools/github/accounts/linking.html) their account.
                                    * If this team is supposed to represent Microsoft and non-Microsoft, the team shouldn't be owned by Microsoft
                                    * If this isn't a Microsoft user, they need to be removed from this team.
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
