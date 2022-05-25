using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting;

internal sealed class OrgReportColumn : ReportColumn
{
    private readonly Func<CachedOrg, string> _selector;

    public OrgReportColumn(string name, string description, Func<CachedOrg, string> selector)
        : base(name, description)
    {
        _selector = selector;
    }

    public override string? GetValue(ReportRow row)
    {
        return row.Org is null ? null : GetValue(row.Org);
    }

    public string GetValue(CachedOrg repo)
    {
        return _selector(repo);
    }
}