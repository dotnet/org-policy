namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR12_BotsShouldBeInTheBotsTeam : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR12",
            "Bots should be in the 'bots' team",
            PolicySeverity.Warning
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            var botsTeam = context.Org.GetBotsTeam();
            if (botsTeam is null)
                return;

            foreach (var user in context.Org.Users)
            {
                var isKnownBot = user.IsBot();
                var isPotentiallyABot = user.IsPotentiallyABot();
                if (!isKnownBot && isPotentiallyABot)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"User '{user.Login}' should be marked as a bot",
                        $@"
                            The user {user.Markdown()} appears to be a bot.

                            * If this is in fact a human, mark this issue as `policy-override` and close the issue
                            * If this is a bot, add it to the team {botsTeam.Markdown()}
                        ",
                        team: botsTeam,
                        user: user
                    );
                }
            }
        }
    }
}
