using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting;

internal sealed class UserReportColumn : ReportColumn
{
    private readonly Func<CachedUser, string?> _selector;

    public UserReportColumn(string name, string description, Func<CachedUser, string?> selector)
        : base(name, description)
    {
        _selector = selector;
    }

    public override string? GetValue(ReportRow row)
    {
        return row.User is null ? null : GetValue(row.User);
    }

    public string? GetValue(CachedUser user)
    {
        return _selector(user);
    }
}