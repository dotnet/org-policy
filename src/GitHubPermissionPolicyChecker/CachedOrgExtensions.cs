using System;
using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker
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

        public static bool IsInMicrosoftTeam(this CachedUser user)
        {
            var microsoftTeam = user.Org.GetMicrosoftTeam();
            if (microsoftTeam == null)
                return false;

            return microsoftTeam.Members.Contains(user);
        }

        public static bool IsClaimingToBeWorkingForMicrosoft(this CachedUser user)
        {
            var companyContainsMicrosoft = user.Company != null &&
                                           user.Company.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;

            var emailContainsMicrosoft = user.Email != null &&
                                         user.Email.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;

            return companyContainsMicrosoft ||
                   emailContainsMicrosoft;
        }

        public static bool IsKnownBot(this CachedUser user)
        {
            return user.Login == "dnfadmin" ||
                   user.Login == "dnfgituser" ||
                   user.Login == "dnfclas";
        }

        public static IEnumerable<CachedUser> GetAdministrators(this CachedRepo repo)
        {
            return repo.Users
                       .Where(ua => ua.Permission == CachedPermission.Admin &&
                                    !ua.Describe().IsOwner) 
                       .Select(ua => ua.User);
        }
    }
}
