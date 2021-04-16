using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR14_RepoOwnershipMustBeExplicit : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR14",
            "Repo ownership must be explicit",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            // If the entire org is owned by Microsoft, we don't need explicit ownership
            if (context.Org.IsOwnedByMicrosoft())
                return;

            var microsoftTeam = context.Org.GetMicrosoftTeam();
            var nonMicrosoftTeam = context.Org.GetNonMicrosoftTeam();

            if (microsoftTeam == null || nonMicrosoftTeam == null)
                return;

            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsArchived)
                    continue;

                // These repos don't live long. There is no point in enforcing ownership semantics.
                if (repo.IsTemporaryForkForSecurityAdvisory())
                    continue;

                var microsoftTeamIsAssigned = repo.Teams.Any(ta => ta.Team == microsoftTeam);
                var nonMicrosoftTeamIsAssigned = repo.Teams.Any(ta => ta.Team == nonMicrosoftTeam);

                var isExplicitlyMarked = microsoftTeamIsAssigned || nonMicrosoftTeamIsAssigned;

                if (!isExplicitlyMarked)
                {
                    var permission = CachedPermission.Read;

                    context.ReportViolation(
                        Descriptor,
                        $"Repo '{repo.Name}' must indicate ownership",
                        $@"
                            The repo {repo.Markdown()} needs to indicate whether it's owned by Microsoft.

                            * **Owned by Microsoft**. Assign the team {microsoftTeam.Markdown()} with {permission.Markdown()} permissions.
                            * **Not owned by Microsoft**. Assign the team {nonMicrosoftTeam.Markdown()} with {permission.Markdown()} permissions.
                        ",
                        repo: repo
                    );
                }
            }
        }
    }
}
