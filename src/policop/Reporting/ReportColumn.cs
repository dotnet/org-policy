using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.Policies;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal abstract class ReportColumn
    {
        public ReportColumn(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public abstract string Prefix { get; }
        public string Name { get; }
        public string QualifiedName => $"{Prefix}:{Name}";
        public string Description { get; }
        public abstract string GetValue(ReportRow row);

        public static ReportColumn Get(string qualifiedName)
        {
            return All.SingleOrDefault(c => string.Equals(c.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<ReportColumn> All => RepoColumns.Cast<ReportColumn>()
                                                                  .Concat(TeamColumns)
                                                                  .Concat(UserColumns)
                                                                  .Concat(TeamMembershipColumns)
                                                                  .Concat(TeamAccessColumns)
                                                                  .Concat(UserAccessColumns);

        public static IReadOnlyList<ReportColumn> RepoColumns { get; } = new[]
        {
            new RepoReportColumn(
                "name",
                "The name of the repo",
                r => r.Name            ),
            new RepoReportColumn(
                "private",
                "Indicates whether the repos is private",
                r => r.IsPrivate ? "Yes" : "No"
            ),
            new RepoReportColumn(
                "archived",
                "Indicates whether the repos is archived",
                r => r.IsArchived ? "Yes" : "No"
            ),
            new RepoReportColumn(
                "last-push",
                "Indicates when the repos was lates pushed to",
                r => r.LastPush.LocalDateTime.ToShortDateString()
            ),
            new RepoReportColumn(
                "ms-owned",
                "Indicates wether the repo is owned by Microsoft",
                r => r.IsOwnedByMicrosoft() ? "Yes" : "No"
            ),
        };

        public static IReadOnlyList<ReportColumn> TeamColumns { get; } = new[]
        {
            new TeamReportColumn(
                "name",
                "The name of the team",
                t => t.Name
            ),
            new TeamReportColumn(
                "full-name",
                "The full name of the team",
                t => t.GetFullName()
            ),
            new TeamReportColumn(
                "marker",
                "Indicates wether the team is considered a marker team",
                t => t.IsMarkerTeam() ? "Yes" : "No"
            ),
            new TeamReportColumn(
                "ms-owned",
                "Indicates wether the team is owned by Microsoft",
                t => t.IsOwnedByMicrosoft() ? "Yes" : "No"
            ),
        };

        public static IReadOnlyList<ReportColumn> UserColumns { get; } = new[]
        {
            new UserReportColumn(
                "login",
                "The GitHub login",
                (u, ls) => u.Login            ),
            new UserReportColumn(
                "owner",
                "Indicates whether the user is an owner",
                (u, ls) => u.IsOwner ? "Yes" : "No"
            ),
            new UserReportColumn(
                "member",
                "Indicates whether the user is an owner",
                (u, ls) => u.IsMember ? "Yes" : "No"
            ),
            new UserReportColumn(
                "external",
                "Indicates whether the user is an owner",
                (u, ls) => u.IsExternal ? "Yes" : "No"
            ),
            new UserReportColumn(
                "name",
                "The name of the GitHub",
                (u, ls) =>
                {
                    if (ls.LinkByLogin.TryGetValue(u.Login, out var l) &&
                        !string.IsNullOrEmpty(l.MicrosoftInfo.PreferredName))
                    {
                        return l.MicrosoftInfo.PreferredName;
                    }

                    return u.Name;
                }
            ),
            new UserReportColumn(
                "email",
                "The email of the user",
                (u, ls) =>
                {
                    if (ls.LinkByLogin.TryGetValue(u.Login, out var l))
                        return l.MicrosoftInfo.EmailAddress;

                    return u.Email;
                }
            ),
            new UserReportColumn(
                "ms-linked",
                "Indicates whether the user is a linked Microsoft user",
                (u, ls) => ls.LinkByLogin.ContainsKey(u.Login) ? "Yes" : "No"
            ),
            new UserReportColumn(
                "ms-login",
                "The Microsoft alias of the user",
                (u, ls) =>
                {
                    if (ls.LinkByLogin.TryGetValue(u.Login, out var l))
                        return l.MicrosoftInfo.Alias;

                    return string.Empty;
                }
            ),
            new UserReportColumn(
                "company",
                "The company name",
                (u, ls) =>
                {
                    if (ls.LinkByLogin.ContainsKey(u.Login))
                        return "Microsoft";

                    return u.Company;
                }
            ),
            new UserReportColumn(
                "bot",
                "Indicates whether the account is considered a bot",
                (u, ls) => u.IsBot() ? "Yes" : "No"
            )
        };

        public static IReadOnlyList<ReportColumn> TeamMembershipColumns { get; } = new[]
        {
            new TeamMembershipReportColumn(
                "maintainer",
                "Indicates whether the user is a maintainer",
                (t, u) => t.Maintainers.Contains(u) ? "Yes" : "No"
            )
        };

        public static IReadOnlyList<ReportColumn> TeamAccessColumns { get; } = new[]
{
            new TeamAccessReportColumn(
                "permission",
                "The permission the team has",
                ta => ta.Permission.ToString().ToLower()
            )
        };

        public static IReadOnlyList<ReportColumn> UserAccessColumns { get; } = new ReportColumn[]
        {
            new UserAccessReportColumn(
                "permission",
                "The permission the user has",
                ua => ua.Permission.ToString().ToLower()
            ),
            new UserAccessReportColumn(
                "reason",
                "The reason why the user has the permission",
                ua => ua.Describe().ToString()
            ),
            new CustomReportColumn(
                "ua",
                "change",
                "The change in permissions",
                r => r.WhatIfPermission?.ToString()
            )
        };
    }
}
