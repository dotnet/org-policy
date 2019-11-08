using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Markdig;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Policies;
using Microsoft.DotnetOrg.PolicyCop.Reporting;
using Microsoft.Office.Interop.Outlook;

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
        private bool _generateEmail;
        private bool _sendEmail;
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
                   .Add("email", "If specified, it will generate emails for affected people", v => _generateEmail = true)
                   .Add("send", "If specified, it will send generated emails", v => _sendEmail = true)
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

            var org = await CacheManager.LoadOrgAsync(_orgName);

            if (org == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-refresh or cache-org first.");
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

            WhatIfDowngraded(org, newPermission);
        }

        private void WhatIfDowngraded(CachedOrg org, CachedPermission? newPermission)
        {
            var repoFilter = _reportContext.CreateRepoFilter();
            var teamFilter = _reportContext.CreateTeamFilter();
            var userFilter = _reportContext.CreateUserFilter();

            var rows = org.Collaborators.Where(ua => userFilter(ua.User))
                          .SelectMany(c => org.Teams.Where(teamFilter), (ua, t) => (UserAccess: ua, Team: t))
                          .Where(t => repoFilter(t.UserAccess.Repo))
                          .Select(t => new ReportRow(repo: t.UserAccess.Repo,
                                                      userAccess: t.UserAccess,
                                                      team: t.Team,
                                                      user: t.UserAccess.User,
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
            else if (_generateEmail)
                SendOrSaveMails(org, rows, newPermission);
            else
                document.PrintToConsole();
        }

        private void SendOrSaveMails(CachedOrg org, ReportRow[] rows, CachedPermission? newPermission)
        {
            var outlookApp = new Application();
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables().Build();

            var userGroups = rows.Where(r => r.WhatIfPermission != null &&
                                              !r.WhatIfPermission.Value.IsUnchanged &&
                                              r.User.IsMicrosoftUser() &&
                                              !string.IsNullOrEmpty(r.User.GetEmail()))
                                 .Select(r => (r.User, r.Repo, WhatIfPermission: r.WhatIfPermission.Value))
                                 .Distinct()
                                 .GroupBy(r => r.User);

            var affectedTeams = rows.Select(r => r.Team)
                                     .Distinct()
                                     .ToArray();

            var excludedAdmins = rows.Where(r => r.WhatIfPermission != null &&
                                                  !r.WhatIfPermission.Value.IsUnchanged &&
                                                  r.WhatIfPermission.Value.UserAccess.Permission == CachedPermission.Admin)
                                      .Select(r => (r.Repo, r.User));
            var excludedAdminsSet = new HashSet<(CachedRepo, CachedUser)>(excludedAdmins);

            foreach (var userGroup in userGroups)
            {
                var user = userGroup.Key;
                var name = user.GetName();
                var email = user.GetEmail();

                var sb = new StringBuilder();
                sb.AppendLine($"Hello {name},");
                sb.AppendLine();
                sb.AppendLine($"We're making some team changes to the {_orgName} GitHub org:");
                sb.AppendLine();

                foreach (var team in affectedTeams)
                {
                    if (newPermission == null)
                        sb.AppendLine($"* The team {team.Name} will be deleted.");
                    else
                        sb.AppendLine($"* The team {team.Name} will now only grant `{newPermission.ToString().ToLower()}` permissions.");
                }

                sb.AppendLine();
                sb.AppendLine("Once this change is in effect, your permissions to these repos will be affected:");
                sb.AppendLine();
                sb.AppendLine("Repo|Change|Repo Admins");
                sb.AppendLine(":---|:-----|:----------");

                foreach (var (_, repo, change) in userGroup)
                {
                    var repoAdmins = repo.GetAdministrators()
                                         .Where(u => u.IsMicrosoftUser() &&
                                                     !excludedAdminsSet.Contains((repo, u)))
                                         .Select(u => (Email: u.GetEmail(), Name: u.GetName()))
                                         .Where(t => !string.IsNullOrEmpty(t.Email) && !string.IsNullOrEmpty(t.Name))
                                         .Select(t => $"[{t.Name}](mailto:{t.Email})");
                    var repoAdminList = string.Join("; ", repoAdmins);

                    sb.Append(repo.Name);
                    sb.Append("|");
                    sb.Append(change.ToString());
                    sb.Append("|");
                    sb.Append(repoAdminList);
                    sb.AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine("If you need more access, please contact the corresponding repo admins so that they can add you to the appropriate team.");
                sb.AppendLine();
                sb.AppendLine("Apologies for any disruption and thank you for your understanding.");
                sb.AppendLine("");
                sb.AppendLine("Thanks!");

                var html = Markdown.ToHtml(sb.ToString(), pipeline);

                var mailItem = (MailItem)outlookApp.CreateItem(OlItemType.olMailItem);
                mailItem.To = email;
                mailItem.ReplyRecipients.Add("dotnetossadmin@microsoft.com");
                mailItem.Subject = $"[Action Required] Your permissions for some repos will change";
                mailItem.HTMLBody = html;

                if (_sendEmail)
                    mailItem.Send();
                else
                    mailItem.Save();
            }
        }
    }
}
