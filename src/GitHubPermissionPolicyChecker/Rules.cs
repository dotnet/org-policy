using System;
using System.Collections.Generic;
using System.Linq;
using Octokit;
using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker
{
    internal abstract class PolicyRule
    {
        public abstract IEnumerable<string> GetViolations(CachedOrg org);
    }

    internal sealed class MicrosoftTeamShouldOnlyGrantReadAccess : PolicyRule
    {
        public override IEnumerable<string> GetViolations(CachedOrg org)
        {
            foreach (var repo in org.Repos)
            {
                foreach (var teamAccess in repo.Teams)
                {
                    if (teamAccess.Permission != CachedPermission.Pull &&
                        teamAccess.Team.Name == "Microsoft")
                    {
                        yield return $"Repo '{repo.Name}' shouldn't grant 'Microsoft' {teamAccess.Permission} permissions.";
                    }
                }
            }
        }
    }

    internal sealed class MicrosoftRepoShouldOnlyGrantReadAccessToExternals : PolicyRule
    {
        public override IEnumerable<string> GetViolations(CachedOrg org)
        {
            var microsoftTeam = org.Teams.SingleOrDefault(t => t.Name == "Microsoft");
            if (microsoftTeam == null)
                yield break;

            var employees = new HashSet<string>(microsoftTeam.Members, StringComparer.OrdinalIgnoreCase);

            var usersAndRepos = org.Repos
                                   .Where(r => r.Teams.Any(ta => ta.Team == microsoftTeam))
                                   .SelectMany(r => r.Users, (Repo, UserAccess) => (Repo, UserAccess))
                                   .Where(t => t.UserAccess.Permission != CachedPermission.Pull &&
                                                !employees.Contains(t.UserAccess.User))
                                   .Select(t => (t.Repo, t.UserAccess.User))
                                   .GroupBy(t => t.User);

            foreach (var group in usersAndRepos)
            {
                var user = group.Key;
                var repo = group.First().Repo.Name;
                var otherRepos = group.Count() - 1;
                var andOthers = otherRepos == 0
                    ? string.Empty
                    : $" (and {otherRepos:N0} other repos)";

                yield return $"External contributor '{user}' was granted more than pull permissions to repo '{repo}'{andOthers}.";
            }
        }
    }
}
