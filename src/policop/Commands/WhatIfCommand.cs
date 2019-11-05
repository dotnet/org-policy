using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;
using Microsoft.DotnetOrg.PolicyCop.Reporting;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class WhatIfCommand : ToolCommand
    {
        private string _orgName;
        private string _newPermissions;
        private List<string> _activeTerms;
        private string _outputFileName;
        private bool _viewInExcel;
        private readonly ReportContext _reportContext = new ReportContext();

        public override string Name => "what-if";

        public override string Description => "Shows the impact when a team's permissions are downgraded";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("p=", "Selects new {permission} for the selected teams", v => _newPermissions = v)
                   .Add("r", "Lists repos", v => { _activeTerms = _reportContext.RepoTerms; })
                   .Add("t", "Lists teams", v => { _activeTerms = _reportContext.TeamTerms; })
                   .Add("u", "Lists user", v => { _activeTerms = _reportContext.UserTerms; })
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("c", "Column names to include", v => _activeTerms = _reportContext.IncludedColumns)
                   .Add("f", "Extra filters", v => _activeTerms = _reportContext.ColumnFilters)
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("<>", v => _activeTerms?.Add(v));
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var org = await CachedOrg.LoadFromCacheAsync(_orgName);

            if (org == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-refresh or cache-org first.");
                return;
            }

            var linkSet = await OspoLinkSet.LoadFromCacheAsync();

            if (linkSet == null)
            {
                Console.Error.WriteLine("error: links not cached yet. Run cache-refresh or cache-links first.");
                return;
            }

            CachedPermission? newPermission;

            if (_newPermissions == "none")
            {
                newPermission = null;
            }
            else if (_newPermissions == "pull")
            {
                newPermission = CachedPermission.Pull;
            }
            else if (_newPermissions == "push")
            {
                newPermission = CachedPermission.Push;
            }
            else if (_newPermissions == "admin")
            {
                newPermission = CachedPermission.Admin;
            }
            else
            {
                Console.Error.WriteLine($"error: permission can be 'none', 'pull', 'push' or 'admin' but not '{_newPermissions}'");
                return;
            }

            WhatIfDowngraded(org, linkSet, newPermission);
        }

        private void WhatIfDowngraded(CachedOrg org, OspoLinkSet linkSet, CachedPermission? newPermission)
        {
            var repoFilter = _reportContext.CreateRepoFilter();
            var teamFilter = _reportContext.CreateTeamFilter();
            var userFilter = _reportContext.CreateUserFilter(linkSet);

            var rows = org.Collaborators.Where(ua => userFilter(ua.User))
                          .SelectMany(c => org.Teams.Where(teamFilter), (ua, t) => (UserAccess: ua, Team: t))
                          .Where(t => repoFilter(t.UserAccess.Repo))
                          .Select(t => new ReportRow(repo: t.UserAccess.Repo,
                                                      userAccess: t.UserAccess,
                                                      team: t.Team,
                                                      user: t.UserAccess.User,
                                                      linkSet: linkSet,
                                                      whatIfPermission: t.UserAccess.WhatIfDowngraded(t.Team, newPermission)))
                          .Where(r => !r.WhatIfPermission.Value.IsUnchanged)
                          .Where(_reportContext.CreateRowFilter())
                          .ToArray();

            var columns = _reportContext.GetColumns("r:name", "t:name", "u:login", "ua:change");
            var document = _reportContext.CreateReport(rows, columns);

            if (!string.IsNullOrEmpty(_outputFileName))
                document.Save(_outputFileName);

            if (_viewInExcel)
                document.ViewInExcel();
            else
                document.PrintToConsole();
        }
    }
}
