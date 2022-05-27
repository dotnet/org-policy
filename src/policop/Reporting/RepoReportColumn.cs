using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting;

internal sealed class RepoReportColumn : ReportColumn
{
    private readonly Func<CachedRepo, string> _selector;

    public RepoReportColumn(string name, string description, Func<CachedRepo, string> selector)
        : base(name, description)
    {
        _selector = selector;
    }

    public override string? GetValue(ReportRow row)
    {
        return row.Repo is null ? null : GetValue(row.Repo);
    }

    public string GetValue(CachedRepo repo)
    {
        return _selector(repo);
    }
}