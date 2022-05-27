using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules;

internal sealed class PR04_MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft : PolicyRule
{
    public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
        "PR04",
        "Team should be owned by Microsoft",
        PolicySeverity.Error
    );

    public override void GetViolations(PolicyAnalysisContext context)
    {
        var maxNonMicrosoftPermission = CachedPermission.Triage;
        var microsoftTeam = context.Org.GetMicrosoftTeam();
        var microsoftTeamMarkdown = microsoftTeam?.Markdown() ?? "Microsoft";

        foreach (var team in context.Org.Teams)
        {
            var isOwnedByMicrosoft = team.IsOwnedByMicrosoft();
            var exceedsMaxForExternals = team.Repos.Any(r => r.Repo.IsOwnedByMicrosoft() && r.Permission > maxNonMicrosoftPermission);

            if (!isOwnedByMicrosoft && exceedsMaxForExternals)
            {
                context.ReportViolation(
                    Descriptor,
                    $"Team '{team.Name}' must be owned by Microsoft",
                    $@"
                            Team {team.Markdown()} grants at least one Microsoft-owned repo more than {maxNonMicrosoftPermission.Markdown()} permissions. The team must be owned by Microsoft.

                            To indicate that the team is owned by Microsoft, ensure that one of the parent teams is {microsoftTeamMarkdown}.
                        ",
                    team: team
                );
            }
        }
    }
}