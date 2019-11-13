using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies
{
    public static class CachedOrgExtensions
    {
        public static CachedTeam GetNonMicrosoftTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "non-microsoft", StringComparison.OrdinalIgnoreCase));
        }

        public static CachedTeam GetMicrosoftTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "microsoft", StringComparison.OrdinalIgnoreCase));
        }

        public static CachedTeam GetMicrosoftVendorsTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "microsoft-vendors", StringComparison.OrdinalIgnoreCase));
        }

        public static CachedTeam GetMicrosoftBotsTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "microsoft-bots", StringComparison.OrdinalIgnoreCase));
        }

        public static CachedTeam GetBotsTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "bots", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsMarkerTeam(this CachedTeam team)
        {
            var org = team.Org;
            return team == org.GetNonMicrosoftTeam() ||
                   team == org.GetMicrosoftTeam() ||
                   team == org.GetMicrosoftVendorsTeam() ||
                   team == org.GetMicrosoftBotsTeam() ||
                   team == org.GetBotsTeam();
        }

        public static bool IsOwnedByMicrosoft(this CachedRepo repo)
        {
            var nonMicrosoftTeam = repo.Org.GetNonMicrosoftTeam();
            var microsoftTeam = repo.Org.GetMicrosoftTeam();

            if (repo.Teams.Any(ta => ta.Team == nonMicrosoftTeam))
                return false;

            return repo.Teams.Any(ta => ta.Team == microsoftTeam);
        }

        public static bool IsOwnedByMicrosoft(this CachedTeam team)
        {
            var microsoftTeam = team.Org.GetMicrosoftTeam();
            return team.AncestorsAndSelf().Any(t => t == microsoftTeam);
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

        public static bool IsMicrosoftUser(this CachedUser user)
        {
            if (user.MicrosoftInfo != null)
                return true;

            var teams = new[]
            {
                user.Org.GetMicrosoftVendorsTeam(),
                user.Org.GetMicrosoftBotsTeam()
            };

            return teams.Any(t => t != null && t.Members.Contains(user));
        }

        public static string GetName(this CachedUser user)
        {
            if (!string.IsNullOrEmpty(user.MicrosoftInfo?.PreferredName))
                return user.MicrosoftInfo.PreferredName;

            return user.Name ?? "@" + user.Login;
        }

        public static string GetEmail(this CachedUser user)
        {
            if (!string.IsNullOrEmpty(user.MicrosoftInfo?.EmailAddress))
                return user.MicrosoftInfo.EmailAddress;

            return user.Email;
        }

        public static string GetEmailName(this CachedUser user)
        {
            var name = user.GetName();
            var email = user.GetEmail();

            if (string.IsNullOrEmpty(email))
                return name;

            return $"{name} <{email}>";
        }

        public static bool IsBot(this CachedUser user)
        {
            var team = user.Org.GetBotsTeam();
            return team != null && team.Members.Contains(user);
        }

        public static bool IsPotentiallyABot(this CachedUser user)
        {
            return user.Login.IndexOf("bot", StringComparison.OrdinalIgnoreCase) > 0;
        }

        public static bool IsUnused(this CachedTeam team)
        {
            var hasChildren = team.Children.Any();
            var hasRepos = team.Repos.Any();
            return !hasChildren && !hasRepos;
        }

        public static IEnumerable<CachedUser> GetOwners(this CachedOrg org)
        {
            return org.Users.Where(u => u.IsOwner && !u.IsBot());
        }

        public static IEnumerable<CachedUser> GetAdministrators(this CachedRepo repo, bool fallbackToOwners = true)
        {
            var result = repo.Users
                             .Where(ua => ua.Permission == CachedPermission.Admin &&
                                          !ua.User.IsBot() &&
                                          !ua.Describe().IsOwner)
                             .Select(ua => ua.User);

            if (fallbackToOwners && !result.Any())
                return repo.Org.GetOwners();

            return result;
        }

        public static IEnumerable<CachedUser> GetMaintainers(this CachedTeam team)
        {
            var result = team.Maintainers.Where(u => !u.IsBot());

            if (!result.Any())
                return team.Org.GetOwners();

            return result;
        }

        public static string Markdown(this CachedRepo repo)
        {
            return $"[{repo.Name}]({repo.Url})";
        }

        public static string Markdown(this CachedTeam team)
        {
            return $"[{team.Name}]({team.Url})";
        }

        public static string Markdown(this CachedUser user)
        {
            return $"[{user.Login}]({user.Url})";
        }

        public static string Markdown(this CachedPermission permission)
        {
            return $"`{permission.ToString().ToLower()}`";
        }
    }
}
