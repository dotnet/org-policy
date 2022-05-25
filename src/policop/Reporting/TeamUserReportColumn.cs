using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class TeamUserReportColumn : ReportColumn
    {
        private readonly Func<CachedTeam, CachedUser, string?> _selector;

        public TeamUserReportColumn(string name, string description, Func<CachedTeam, CachedUser, string?> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string? GetValue(ReportRow row)
        {
            return row.Team is null || row.User is null ? null : GetValue(row.Team, row.User);
        }

        public string? GetValue(CachedTeam team, CachedUser user)
        {
            return _selector(team, user);
        }
    }
}
