using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class TeamReportColumn : ReportColumn
    {
        private readonly Func<CachedTeam, string?> _selector;

        public TeamReportColumn(string name, string description, Func<CachedTeam, string?> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string? GetValue(ReportRow row)
        {
            return row.Team is null ? null : GetValue(row.Team);
        }

        public string? GetValue(CachedTeam team)
        {
            return _selector(team);
        }
    }
}
