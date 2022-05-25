namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR25_MicrosoftOwnedPrivateReposNotShouldGrantAccessViaNonMicrosoftTeams : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR25",
            "Microsoft-owned private repos should not grant access via any non-Microsoft owned teams",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                if (!repo.IsOwnedByMicrosoft())
                    continue;

                if (!repo.IsPrivate)
                    continue;

                var nonMicrosoftTeamAccess = repo.Teams.Where(t => !t.Team.IsOwnedByMicrosoft() &&
                                                                   !t.Team.IsInfrastructure());

                foreach (var access in nonMicrosoftTeamAccess)
                {
                    var team = access.Team;
                    var permission = access.Permission;

                    context.ReportViolation(
                        Descriptor,
                        $"Microsoft owned private repo '{repo.Name}' should not grant access via non-Microsoft owned team '{team.GetFullSlug()}'",
                        $@"
                        The repo {repo.Markdown()} gives {permission.Markdown()} access to a non-Microsoft owned team {team.Markdown()}. Either remove the team from the repo or make the team owned by Microsoft. Alternatively, if giving access is intentional, provide a justification below and apply an explicit policy override.
                        ",
                        repo: repo,
                        team: team
                    );
                }
            }
        }
    }
}
