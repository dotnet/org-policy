using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedUserAccess
    {
        public string RepoName { get; set; }
        public string UserLogin { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedOrg Org => Repo.Org;

        [JsonIgnore]
        public CachedRepo Repo { get; set; }

        [JsonIgnore]
        public CachedUser User { get; set; }

        public CachedAccessReason Describe()
        {
            foreach (var teamAccess in Repo.Teams)
            {
                if (teamAccess.Permission == Permission)
                {
                    foreach (var team in teamAccess.Team.DescendentsAndSelf())
                    {
                        if (team.Members.Contains(User))
                            return CachedAccessReason.FromTeam(team);
                    }
                }
            }

            return User.IsOwner
                    ? CachedAccessReason.FromOwner
                    : CachedAccessReason.FromCollaborator;
        }

        public CachedWhatIfPermission WhatIfDowngraded(CachedTeam team, CachedPermission? newPermission)
        {
            return WhatIf(ta =>
            {
                if (ta.Team == team)
                {
                    if (newPermission == null)
                        return null;

                    // Only downgrade, never upgrade
                    if (ta.Permission >= newPermission.Value)
                        return newPermission.Value;
                }

                return ta.Permission;
            });
        }

        public CachedWhatIfPermission WhatIf(Func<CachedTeamAccess, CachedPermission?> permissionChanger)
        {
            if (User.IsOwner)
                return new CachedWhatIfPermission(this, CachedPermission.Admin);

            // Let's start by computing what we're getting from the repo directly.
            //
            // NOTE: This currently won't work because the GitHub API has no way to tell us
            //       direct repo permissions. For detais, see:
            //
            //              https://github.com/octokit/octokit.net/issues/2036)
            //
            //       Rather, it only gives us effective permissions. This means that if a user
            //       has 'admin' permissions through a team, but 'write' permissions by directly
            //       being added to a repo, running what-if for this repo/team will (incorrectly)
            //       conclude that the user was downgraded to 'read' or lost access (if the repo
            //       is private).
            //
            //       However, the current code will work for cases where the permissions granted
            //       through the team is less than what was given via the repo directly.

            var maximumLevel = Repo.Users.Where(ua => ua.User == User && ua.Describe().IsCollaborator)
                                          .Select(ua => (int)ua.Permission)
                                          .DefaultIfEmpty(-1)
                                          .Max();

            foreach (var teamAccess in Repo.Teams)
            {
                var teamAccessLevel = (int)teamAccess.Permission;

                foreach (var nestedTeam in teamAccess.Team.DescendentsAndSelf())
                {
                    if (!nestedTeam.Members.Contains(User))
                        continue;

                    var newPermission = permissionChanger(teamAccess);
                    var newPermissionLevel = newPermission == null ? -1 : (int)newPermission.Value;
                    maximumLevel = Math.Max(maximumLevel, newPermissionLevel);
                    break;
                }
            }

            if (maximumLevel == -1)
                return new CachedWhatIfPermission(this, Repo.IsPrivate ? null : (CachedPermission?)CachedPermission.Read);

            var maximumPermission = (CachedPermission)maximumLevel;
            return new CachedWhatIfPermission(this, maximumPermission);
        }
    }
}
