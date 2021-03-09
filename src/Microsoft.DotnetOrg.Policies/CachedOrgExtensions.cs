using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies
{
    public static class CachedOrgExtensions
    {
        public static CachedTeam GetExternalPartnerTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "external-partner", StringComparison.OrdinalIgnoreCase));
        }

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

        public static CachedTeam GetExternalCiAccessTeam(this CachedOrg org)
        {
            return org.Teams.SingleOrDefault(t => string.Equals(t.Name, "external-ci-access", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsMarkerTeam(this CachedTeam team)
        {
            var org = team.Org;
            return team == org.GetExternalPartnerTeam() ||
                   team == org.GetNonMicrosoftTeam() ||
                   team == org.GetMicrosoftTeam() ||
                   team == org.GetMicrosoftVendorsTeam() ||
                   team == org.GetMicrosoftBotsTeam() ||
                   team == org.GetBotsTeam() ||
                   team == org.GetExternalCiAccessTeam();
        }

        public static bool IsUnderDotNetFoundation(this CachedOrg org)
        {
            return string.Equals(org.Name, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(org.Name, "aspnet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(org.Name, "nuget", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(org.Name, "mono", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUnderDotNetFoundation(this CachedRepo repo)
        {
            return repo.Org.IsUnderDotNetFoundation();
        }

        public static bool IsOwnedByMicrosoft(this CachedOrg org)
        {
            // dotnet and mono aren't fully owned by MS
            return string.Equals(org.Name, "microsoft", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(org.Name, "aspnet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(org.Name, "nuget", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOwnedByMicrosoft(this CachedRepo repo)
        {
            var nonMicrosoftTeam = repo.Org.GetNonMicrosoftTeam();
            if (repo.Teams.Any(ta => ta.Team == nonMicrosoftTeam))
                return false;

            if (repo.Org.IsOwnedByMicrosoft())
                return true;

            var microsoftTeam = repo.Org.GetMicrosoftTeam();
            return repo.Teams.Any(ta => ta.Team == microsoftTeam);
        }

        public static bool IsOwnedByMicrosoft(this CachedTeam team)
        {
            var nonMicrosoftTeam = team.Org.GetNonMicrosoftTeam();
            if (team.AncestorsAndSelf().Any(t => t == nonMicrosoftTeam))
                return false;

            if (team.Org.IsOwnedByMicrosoft())
                return true;

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

            if (user.IsKnownMicrosoftServiceAccount())
                return true;

            var teams = new[]
            {
                user.Org.GetMicrosoftVendorsTeam(),
                user.Org.GetMicrosoftBotsTeam()
            };

            return teams.Any(t => t != null && t.EffectiveMembers.Contains(user));
        }

        private static bool IsKnownMicrosoftServiceAccount(this CachedUser user)
        {
            // Due to a recent security push in the Microsoft org, many
            // service accounts have been demoted to external contributors
            // which means they can't be team members anymore. Thus only
            // checking membership in the microsoft-bots team doesn't work
            // anymore.
            //
            // Solution: we'll hard code them here.

            var knownServiceAccounts = new[]
            {
                "cxwtool"
            };

            return knownServiceAccounts.Any(a => string.Equals(user.Login, a, StringComparison.OrdinalIgnoreCase));
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
            if (user.IsKnownMicrosoftServiceAccount())
                return true;

            var team = user.Org.GetBotsTeam();
            return team != null && team.EffectiveMembers.Contains(user);
        }

        public static bool IsPotentiallyABot(this CachedUser user)
        {
            return user.Login.IndexOf("bot", StringComparison.OrdinalIgnoreCase) > 0;
        }

        public static bool IsUnused(this CachedTeam team)
        {
            if (team.IsMarkerTeam())
                return false;

            // If a team is marked as being an external partner team, we don't consider
            // it unused. The rationale is that:
            //
            //    (1) organizing the lists of external poeple is work that we don't want
            //        to redo
            //
            //    (2) these teams are often used only temporary in order to grant access
            //        to early design documents or previews
            //
            var externalPartnerTeam = team.Org.GetExternalPartnerTeam();
            if (externalPartnerTeam != null && team.AncestorsAndSelf().Contains(externalPartnerTeam))
                return false;

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
            var result = repo.EffectiveUsers
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

        public static bool IsSoftArchived(this CachedRepo repo)
        {
            return string.Equals(repo.DefaultBranch, "archive", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasMasterBranch(this CachedRepo repo)
        {
            return repo.Branches.Any(n => string.Equals(n, "master", StringComparison.OrdinalIgnoreCase));
        }

        public static bool MigratedToMainBranch(this CachedRepo repo)
        {
            return !repo.HasMasterBranch() || repo.IsArchived || repo.IsSoftArchived();
        }

        public static string MigrationToMainStatus(this CachedRepo repo)
        {
            if (repo.IsArchived || repo.IsSoftArchived())
                return "Archived";

            if (repo.MigratedToMainBranch())
                return "Completed";

            return "Pending";
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
