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
        private string _command;
        private string _newPermissions;
        private List<string> _activeTerms;
        private string _outputFileName;
        private bool _viewInExcel;
        private readonly ReportContext _reportContext = new ReportContext();

        public override string Name => "what-if";

        public override string Description => "Shows the impact when a user is removed from a particular team";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("team-user-remove", "Checks what happens if the selected users are removed from the selected teams", v => _command = "team-user-remove")
                   .Add("team-repo-grant=", "Checks what happens if the selected users are removed from the selected teams", v => { _command = "team-repo-grant"; _newPermissions = v; })
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

            if (_command == null)
            {
                Console.Error.WriteLine($"error: must specify an operation (e.g. --team-user-remove)");
                return;
            }

            if (_command == "team-user-remove")
            {
                WhatIf(org, linkSet, null);
            }
            else if (_command == "team-repo-grant")
            {
                CachedPermission newPermission;
                if (_newPermissions == "pull")
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
                    Console.Error.WriteLine($"error: permission can be 'pull', 'push' or 'admin' but not '{_newPermissions}'");
                    return;
                }

                WhatIf(org, linkSet, newPermission);
            }
            else
            {
                Debug.Assert(false);
            }
        }

        private void WhatIf(CachedOrg org, OspoLinkSet linkSet, CachedPermission? newPermission)
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
                                                      whatIfPermission: t.UserAccess.WhatIf(t.Team, newPermission)))
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
