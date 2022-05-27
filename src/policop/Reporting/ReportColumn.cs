using Microsoft.DotnetOrg.Policies;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting;

internal abstract class ReportColumn
{
    public ReportColumn(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }
    public string Description { get; }
    public abstract string? GetValue(ReportRow row);

    public static ReportColumn? Get(string name)
    {
        return All.SingleOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<ReportColumn> All => OrgColumns.Concat(RepoColumns)
                                                             .Concat(TeamColumns)
                                                             .Concat(UserColumns)
                                                             .Concat(TeamMembershipColumns)
                                                             .Concat(TeamAccessColumns)
                                                             .Concat(UserAccessColumns)
                                                             .Concat(AuditColumns);

    public static IReadOnlyList<ReportColumn> OrgColumns { get; } = new[]
    {
        new OrgReportColumn(
            "o:name",
            "The name of the org",
            o => o.Name
        ),
    };

    public static IReadOnlyList<ReportColumn> RepoColumns { get; } = new[]
    {
        new RepoReportColumn(
            "r:name",
            "The name of the repo",
            r => r.Name
        ),
        new RepoReportColumn(
            "r:full-name",
            "The name of the org and repo, separated by a slash",
            r => $"{r.Org.Name}/{r.Name}"
        ),
        new RepoReportColumn(
            "r:private",
            "Indicates whether the repo is private",
            r => r.IsPrivate ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:fork",
            "Indicates whether the repo is a fork",
            r => r.IsFork ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:mirror",
            "Indicates whether the repo is a mirror",
            r => r.IsMirror ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:archived",
            "Indicates whether the repo is archived",
            r => r.IsArchived ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:soft-archived",
            "Indicates whether the repo is soft archived, i.e. wheter it use 'archive' as the default branch",
            r => r.IsSoftArchived() ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:template",
            "Indicates whether the repo is a template repo",
            r => r.IsTemplate ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:last-push",
            "The date when the repo was last pushed to",
            r => r.LastPush.LocalDateTime.ToShortDateString()
        ),
        new RepoReportColumn(
            "r:ms-owned",
            "Indicates whether the repo is owned by Microsoft",
            r => r.IsOwnedByMicrosoft() ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:admins",
            "Names and emails of admins",
            r => string.Join("; ", r.GetAdministrators().Select(u => u.GetEmailName()))
        ),
        new RepoReportColumn(
            "r:assign-admin",
            "Randomly selects an admin",
            r =>
            {
                // First, let's only look at MS admins
                var admins = r.GetAdministrators()
                    .Where(a => !string.IsNullOrEmpty(a.MicrosoftInfo?.EmailAddress))
                    .Select(u => u.GetEmailName())
                    .ToArray();

                // If we don't find any let's look at anyone with an email
                if (admins.Length == 0)
                {
                    admins = r.GetAdministrators()
                        .Where(a => !string.IsNullOrEmpty(a.GetEmail()))
                        .Select(u => u.GetEmailName())
                        .ToArray();

                    // OK, we're out of luck
                    if (admins.Length == 0)
                        return string.Empty;
                }

                // Now assign an random one
                return admins[Random.Shared.Next(0, admins.Length)];
            }
        ),
        new RepoReportColumn(
            "r:description",
            "Description of the repo",
            r => r.Description
        ),
        new RepoReportColumn(
            "r:has-master",
            "Indicates whether the repo still has a branch called 'master'",
            r => r.HasMasterBranch() ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:migrated-to-main",
            "Indicates whether the repo is considered having migrated to using a 'main' branch",
            r => r.MigratedToMainBranch() ? "Yes" : "No"
        ),
        new RepoReportColumn(
            "r:migration-to-main-status",
            "The status of the main migration ('Pending', 'Completed', or 'Archived')",
            r => r.MigrationToMainStatus()
        ),
        new RepoReportColumn(
            "r:action-permissions",
            "The configuration which GitHub actions can be used",
            r => r.ActionPermissions.AllowedActions.ToString()
        ),
        new RepoReportColumn(
            "r:has-workflows",
            "Indicates whether this repo has any GitHub action workflows",
            r => r.Workflows.Any() ? "Yes" : "No"
        ),
    };

    public static IReadOnlyList<ReportColumn> TeamColumns { get; } = new[]
    {
        new TeamReportColumn(
            "t:name",
            "The name of the team",
            t => t.Name
        ),
        new TeamReportColumn(
            "t:slug",
            "The team slug (the slug is the name that is used in the URL)",
            t => t.Slug
        ),
        new TeamReportColumn(
            "t:parent-name",
            "The name of the parent team",
            t => t.Parent?.Name
        ),
        new TeamReportColumn(
            "t:full-slug",
            "The slug of the team prefixed with all parent slugs",
            t => t.GetFullSlug()
        ),
        new TeamReportColumn(
            "t:full-name",
            "The name of the team prefixed with all parent teams",
            t => t.GetFullName()
        ),
        new TeamReportColumn(
            "t:marker",
            "Indicates whether the team is considered a marker team",
            t => t.IsMarkerTeam() ? "Yes" : "No"
        ),
        new TeamReportColumn(
            "t:ms-owned",
            "Indicates whether the team is owned by Microsoft",
            t => t.IsOwnedByMicrosoft() ? "Yes" : "No"
        ),
        new TeamReportColumn(
            "t:ms-only-members",
            "Indicates whether the team has only members from Microsoft",
            t => t.Members.All(m => m.IsMicrosoftUser()) ? "Yes" : "No"
        ),
        new TeamReportColumn(
            "t:maintainers",
            "Names and emails of maintainers",
            t => string.Join("; ", t.GetMaintainers().Select(u => u.GetEmailName()))
        ),
        new TeamReportColumn(
            "t:description",
            "Description for the team",
            t => t.Description
        ),
        new TeamReportColumn(
            "t:is-secret",
            "Indicates whether this team can be seen by all org members or just owners and members",
            t => t.IsSecret ? "Yes" : "No"
        ),
        new TeamReportColumn(
            "t:is-unused",
            "Indicates whether this team has no child teams and isn't assigned to any repos",
            t => t.IsUnused() ? "Yes" : "No"
        ),
    };

    public static IReadOnlyList<ReportColumn> UserColumns { get; } = new[]
    {
        new UserReportColumn(
            "u:login",
            "The GitHub login of the user",
            u => u.Login            ),
        new UserReportColumn(
            "u:owner",
            "Indicates whether the user is an org owner",
            u => u.IsOwner ? "Yes" : "No"
        ),
        new UserReportColumn(
            "u:member",
            "Indicates whether the user is an org owner or member",
            u => u.IsMember ? "Yes" : "No"
        ),
        new UserReportColumn(
            "u:external",
            "Indicates whether the user is not an org member",
            u => u.IsExternal ? "Yes" : "No"
        ),
        new UserReportColumn(
            "u:name",
            "The name of the user",
            u => u.GetName()
        ),
        new UserReportColumn(
            "u:email",
            "The email of the user",
            u => u.GetEmail()
        ),
        new UserReportColumn(
            "u:ms-linked",
            "Indicates whether the user is a linked Microsoft user",
            u => u.MicrosoftInfo is not null ? "Yes" : "No"
        ),
        new UserReportColumn(
            "u:ms-login",
            "The Microsoft alias of the user",
            u => u.MicrosoftInfo?.Alias ?? string.Empty
        ),
        new UserReportColumn(
            "u:company",
            "The company name",
            u => u.MicrosoftInfo is not null ? "Microsoft" : u.Company
        ),
        new UserReportColumn(
            "u:bot",
            "Indicates whether the account is considered a bot",
            u => u.IsBot() ? "Yes" : "No"
        )
    };

    public static IReadOnlyList<ReportColumn> TeamMembershipColumns { get; } = new[]
    {
        new TeamUserReportColumn(
            "tu:maintainer",
            "Indicates whether the user is a maintainer",
            (t, u) => t.Maintainers.Contains(u) ? "Yes" : "No"
        )
    };

    public static IReadOnlyList<ReportColumn> TeamAccessColumns { get; } = new[]
    {
        new TeamAccessReportColumn(
            "rt:permission",
            "The permission the team has",
            ta => ta.Permission.ToString().ToLower()
        )
    };

    public static IReadOnlyList<ReportColumn> UserAccessColumns { get; } = new ReportColumn[]
    {
        new UserAccessReportColumn(
            "ru:permission",
            "The permission the user has",
            ua => ua.Permission.ToString().ToLower()
        ),
        new UserAccessReportColumn(
            "ru:reason",
            "The reason why the user has the permission",
            ua => ua.Describe().ToString()
        ),
        new RowReportColumn(
            "ru:change",
            "The change in permissions",
            r => r.WhatIfPermission?.ToString()
        )
    };

    public static IReadOnlyList<ReportColumn> AuditColumns { get; } = new ReportColumn[]
    {
        new RowReportColumn(
            "rtu:principal-kind",
            "Indicates whether it's a user or team",
            r => {
                if (r.Team is not null && r.User is null)
                    return "Team";
                else if (r.Team is null && r.User is not null)
                    return "User";
                return null;
            }
        ),
        new RowReportColumn(
            "rtu:principal",
            "The user or team",
            r => {
                if (r.Team is not null && r.User is null)
                    return r.Team.Name;
                else if (r.Team is null && r.User is not null)
                    return r.User.Login;
                return null;
            }
        ),
        new RowReportColumn(
            "rtu:permission",
            "The permission of the user or team",
            r => {
                if (r.TeamAccess is not null && r.UserAccess is null)
                    return r.TeamAccess.Permission.ToString().ToLower();
                else if (r.TeamAccess is null && r.UserAccess is not null)
                    return r.UserAccess.Permission.ToString().ToLower();
                return null;
            }
        ),
    };
}