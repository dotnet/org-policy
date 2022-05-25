using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class TeamAccessReportColumn : ReportColumn
    {
        private readonly Func<CachedTeamAccess, string> _selector;

        public TeamAccessReportColumn(string name, string description, Func<CachedTeamAccess, string> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string? GetValue(ReportRow row)
        {
            return row.TeamAccess is null ? null : GetValue(row.TeamAccess);
        }

        public string GetValue(CachedTeamAccess teamAccess)
        {
            return _selector(teamAccess);
        }
    }
}
