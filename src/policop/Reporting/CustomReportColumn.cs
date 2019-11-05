using System;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class CustomReportColumn : ReportColumn
    {
        private readonly Func<ReportRow, string> _selector;

        public CustomReportColumn(string prefix, string name, string description, Func<ReportRow, string> selector)
            : base(name, description)
        {
            Prefix = prefix;
            _selector = selector;
        }

        public override string Prefix { get; }

        public override string GetValue(ReportRow row)
        {
            return _selector(row);
        }
    }
}
