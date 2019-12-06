using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR14_RepoOwnershipMustBeExplicit : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR14",
            "Repo ownership must be explicit",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            // If the entire org is owned by Microsoft, we don't need explicit ownership
            if (context.Org.IsOwnedByMicrosoft())
                yield break;

            var microsoftTeam = context.Org.GetMicrosoftTeam();
            var nonMicrosoftTeam = context.Org.GetNonMicrosoftTeam();

            if (microsoftTeam == null || nonMicrosoftTeam == null)
                yield break;

            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsArchived)
                    continue;

                var microsoftTeamIsAssigned = repo.Teams.Any(ta => ta.Team == microsoftTeam);
                var nonMicrosoftTeamIsAssigned = repo.Teams.Any(ta => ta.Team == nonMicrosoftTeam);

                var isExplicitlyMarked = microsoftTeamIsAssigned || nonMicrosoftTeamIsAssigned;

                if (!isExplicitlyMarked)
                {
                    var permission = CachedPermission.Read;

                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Repo '{repo.Name}' must indicate ownership",
                        body: $@"
                            The repo {repo.Markdown()} needs to indicate whether it's owned by Microsoft.

                            * **Owned by Microsoft**. Assign the team {microsoftTeam.Markdown()} with {permission.Markdown()} permissions.
                            * **Not owned by Microsoft**. Assign the team {nonMicrosoftTeam.Markdown()} with {permission.Markdown()} permissions.
                        ",
                        org: context.Org,
                        repo: repo
                    );
                }
            }
        }
    }
}
