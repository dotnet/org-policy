using System;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal sealed class RowReportColumn : ReportColumn
    {
        private readonly Func<ReportRow, string> _selector;

        public RowReportColumn(string name, string description, Func<ReportRow, string> selector)
            : base(name, description)
        {
            _selector = selector;
        }

        public override string GetValue(ReportRow row)
        {
            return _selector(row);
        }
    }
}
