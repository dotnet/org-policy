using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR13_CollaboratorAccessIsSuperfluous : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR13",
            "Collaborator access is superfluous",
            PolicySeverity.Warning
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                var orgOwnersOrTeamUsers = new Dictionary<CachedUser, CachedUserAccess>();

                foreach (var teamAccess in repo.Teams)
                {
                    foreach (var user in teamAccess.Team.EffectiveMembers)
                    {
                        if (orgOwnersOrTeamUsers.TryGetValue(user, out var userAccess))
                        {
                            if (userAccess.Permission >= teamAccess.Permission)
                                continue;
                        }

                        orgOwnersOrTeamUsers[user] = new CachedUserAccess
                        {
                            Repo = repo,
                            RepoName = repo.Name,
                            User = user,
                            UserLogin = user.Login,
                            Permission = teamAccess.Permission
                        };
                    }
                }

                foreach (var collaboratorAccess in repo.Users)
                {
                    var user = collaboratorAccess.User;
                    var permission = collaboratorAccess.Permission;

                    if (user.IsOwner)
                    {
                        yield return new PolicyViolation(
                            Descriptor,
                            title: $"Collborator access for user '{user.Login}' is superfluous",
                            body: $@"
                                In repo {repo.Markdown()} the user {user.Markdown()} was granted {permission.Markdown()} as a collaborator but the user is an organization owner.

                                You should remove the collaborator access.
                            ",
                            org: context.Org,
                            repo: repo,
                            user: user
                        );
                    }
                    else if (orgOwnersOrTeamUsers.TryGetValue(user, out var teamUserAccess) &&
                             permission <= teamUserAccess.Permission)
                    {
                        var teamPermission = teamUserAccess.Permission;
                        var teams = repo.Teams.Where(ta => ta.Permission == teamUserAccess.Permission &&
                                                            ta.Team.EffectiveMembers.Contains(user))
                                              .Select(ta => ta.Team.Markdown());

                        var teamListMarkdown = string.Join(", ", teams);
                        yield return new PolicyViolation(
                            Descriptor,
                            title: $"Collborator access for user '{user.Login}' is superfluous",
                            body: $@"
                                In repo {repo.Markdown()} the user {user.Markdown()} was granted {permission.Markdown()} as a collaborator but the user already has {teamPermission.Markdown()} permissions via the team(s) {teamListMarkdown}.

                                You should remove the collaborator access.
                            ",
                            org: context.Org,
                            repo: repo,
                            user: user
                        );
                    }
                }
            }
        }
    }
}
