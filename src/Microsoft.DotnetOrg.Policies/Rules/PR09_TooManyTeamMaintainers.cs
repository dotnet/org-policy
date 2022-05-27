namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR09_TooManyTeamMaintainers : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR09",
        "Too many team maintainers",
        PolicySeverity.Error
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        const int Threshold = 10;

        foreach (var team in context.Org.Teams)
        {
            var numberOfMaintainers = team.Maintainers.Count(m => !m.IsOwner);

            if (numberOfMaintainers > Threshold)
            {
                context.ReportViolation(
                    Descriptor,
                    $"Team '{team.Name}' has too many maintainers",
                    $@"
                            The team {team.Markdown()} has {numberOfMaintainers} maintainers. Reduce the number of maintainers to {Threshold} or less.
                        ",
                    team: team
                );
            }
        }
    }
}