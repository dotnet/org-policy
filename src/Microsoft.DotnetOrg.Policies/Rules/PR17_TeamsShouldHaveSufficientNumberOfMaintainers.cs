namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR17_TeamsShouldHaveSufficientNumberOfMaintainers : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR17",
            "Teams should have a sufficient number of maintainers",
            PolicySeverity.Warning
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 2;
            foreach (var team in context.Org.Teams)
            {
                if (!team.IsOwnedByMicrosoft())
                    continue;

                var teamThreshold = Math.Min(Threshold, team.Members.Count);
                var numberOfMaintainers = team.GetMaintainers().Count();

                if (numberOfMaintainers < teamThreshold)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Team '{team.Name}' needs more maintainers",
                        $@"
                            The team {team.Markdown()} has {numberOfMaintainers} maintainers. It should have at least {teamThreshold} maintainers.
                        ",
                        team: team
                    );
                }
            }
        }
    }
}
