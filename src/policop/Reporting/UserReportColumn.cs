using System;

using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class UserReportColumn : ReportColumn
    {
        private readonly Func<CachedUser, OspoLinkSet, string> _selector;

        public UserReportColumn(string name, string description, Func<CachedUser, OspoLinkSet, string> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string Prefix => "u";

        public override string GetValue(ReportRow row)
        {
            return row.User == null || row.LinkSet == null ? null : GetValue(row.User, row.LinkSet);
        }

        public string GetValue(CachedUser user, OspoLinkSet linkSet)
        {
            return _selector(user, linkSet);
        }
    }
}
