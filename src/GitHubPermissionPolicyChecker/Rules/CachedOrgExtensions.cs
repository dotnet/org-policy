using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal static class CachedOrgExtensions
    {
        public static CachedTeam GetMicrosoftTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => t.Name == "Microsoft");
        }

        public static bool IsOwnedByMicrosoft(this CachedRepo repo)
        {
            var microsoftTeam = repo.Org.GetMicrosoftTeam();
            return repo.Teams.Any(ta => ta.Team == microsoftTeam);
        }

        public static bool IsOwnedByMicrosoft(this CachedTeam team)
        {
            var microsoftTeam = team.Org.GetMicrosoftTeam();
            return team.AncestorsAndSelf().Any(t => t == microsoftTeam);
        }

        public static bool IsEmployee(this CachedOrg org, string user)
        {
            var microsoftTeam = org.GetMicrosoftTeam();
            if (microsoftTeam == null)
                return false;

            return microsoftTeam.Members.Contains(user);
        }

        public static bool IsKnownBot(this CachedOrg org, string user)
        {
            return user == "dnfadmin" ||
                   user == "dnfgituser" ||
                   user == "dnfclas";
        }
    }
}
