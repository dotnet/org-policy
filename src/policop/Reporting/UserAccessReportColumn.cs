using System;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class UserAccessReportColumn : ReportColumn
    {
        private readonly Func<CachedUserAccess, string?> _selector;

        public UserAccessReportColumn(string name, string description, Func<CachedUserAccess, string?> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string? GetValue(ReportRow row)
        {
            return row.UserAccess is null ? null : GetValue(row.UserAccess);
        }

        public string? GetValue(CachedUserAccess userAccess)
        {
            return _selector(userAccess);
        }
    }
}
