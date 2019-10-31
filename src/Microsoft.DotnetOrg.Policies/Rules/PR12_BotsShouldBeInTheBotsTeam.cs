using System.Collections.Generic;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR12_BotsShouldBeInTheBotsTeam : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR12",
            "Bots should be in the 'bots' team",
            PolicySeverity.Warning
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            var botsTeam = context.Org.GetBotsTeam();
            if (botsTeam == null)
                yield break;

            foreach (var user in context.Org.Users)
            {
                var isKnownBot = user.IsBot();
                var isPotentiallyABot = user.IsPotentiallyABot();
                if (!isKnownBot && isPotentiallyABot)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"User '{user.Login}' should be marked as a bot",
                        body: $@"
                            The user {user.Markdown()} appears to be a bot.

                            * If this is in fact a human, mark this issue as `policy-override` and close the issue
                            * If this is a bot, add it to the team {botsTeam.Markdown()}
                        ",
                        org: context.Org,
                        team: botsTeam,
                        user: user
                    );
                }
            }
        }
    }
}
