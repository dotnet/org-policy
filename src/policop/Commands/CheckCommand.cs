using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Policies;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class CheckCommand : ToolCommand
    {
        private string _orgName;
        private string _outputFileName;
        private string _gitHubToken;
        private string _policyRepo;
        private bool _updateIssues;
        private bool _viewInExcel;

        public override string Name => "check";

        public override string Description => "Checks for policy violations and optionally files issues";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("github-token=", "The GitHub API {token} to be used.", v => _gitHubToken = v)
                   .Add("policy-repo=", "The GitHub {repo} policy violations should be filed in.", v => _policyRepo = v)
                   .Add("update-issues", "Will create, repopen and closed policy violations.", v => _updateIssues = true);
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
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-build or cache-org first.");
                return;
            }

            if (_updateIssues && string.IsNullOrEmpty(_policyRepo))
            {
                Console.Error.WriteLine($"error: --policy-repo must be specified if --update-issues is specified.");
                return;
            }

            if (!RepoName.TryParse(_policyRepo, out var policyRepo))
            {
                Console.Error.WriteLine($"error: policy repo must be of form owner/name but was '{_policyRepo}'.");
                return;
            }

            var gitHubClient = string.IsNullOrEmpty(_policyRepo)
                                ? null
                                : await GitHubClientFactory.CreateAsync(_gitHubToken);

            var context = new PolicyAnalysisContext(org);
            var violations = PolicyRunner.Run(context);

            var report = gitHubClient == null
                            ? ViolationReport.Create(violations)
                            : await CreateViolationReportAsync(gitHubClient, policyRepo, violations);

            SaveVioloations(_orgName, _outputFileName, _viewInExcel, report);

            if (_updateIssues)
                await UpdateIssuesAsync(gitHubClient, policyRepo, report);

            Console.WriteLine($"  Existing violations: {report.ExistingViolations.Count:N0}");
            Console.WriteLine($"Overridden violations: {report.OverriddenViolations.Count:N0}");
            Console.WriteLine($"   Created violations: {report.CreatedViolations.Count:N0}");
            Console.WriteLine($"  Reopened violations: {report.ReopenedViolations.Count:N0}");
            Console.WriteLine($"    Closed violations: {report.ClosedViolations.Count:N0}");
        }

        private static readonly string AreaViolationLabel = "area-violation";
        private static readonly string PolicyOverrideLabel = "policy-override";

        private static void SaveVioloations(string orgName, string outputFileName, bool viewInExcel, ViolationReport report)
        {
            var document = new CsvDocument("org", "status", "severity", "rule", "rule-title", "fingerprint", "violation", "repo", "user", "team", "assignees");

            using (var writer = document.Append())
            {
                foreach (var (status, violation, _) in report.GetAll())
                {
                    if (violation == null)
                        continue;

                    writer.Write(orgName);
                    writer.Write(status);
                    writer.Write(violation.Descriptor.Severity.ToString());
                    writer.Write(violation.Descriptor.DiagnosticId);
                    writer.Write(violation.Descriptor.Title);
                    writer.Write(violation.Fingerprint.ToString());
                    writer.Write(violation.Title);

                    if (violation.Repo == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Repo.Url, violation.Repo.Name, viewInExcel);

                    if (violation.User == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.User.Url, violation.User.Login, viewInExcel);

                    if (violation.Team == null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Team.Url, violation.Team.Name, viewInExcel);

                    var assignees = string.Join(", ", violation.Assignees.Select(r => r.Login));
                    writer.Write(assignees);

                    writer.WriteLine();
                }
            }

            if (!string.IsNullOrEmpty(outputFileName))
                document.Save(outputFileName);

            if (viewInExcel)
                document.ViewInExcel();
        }

        private static async Task<ViolationReport> CreateViolationReportAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            var existingIssues = await GetIssuesAsync(client, policyRepo);
            return ViolationReport.Create(violations, existingIssues);
        }

        private static async Task<IReadOnlyList<PolicyIssue>> GetIssuesAsync(GitHubClient client, RepoName policyRepo)
        {
            await client.PrintProgressAsync(Console.Out, "Loading issue list");
            var issueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.All
            };
            issueRequest.Labels.Add(AreaViolationLabel);
            var existingIssues = await client.Issue.GetAllForRepository(policyRepo.Owner, policyRepo.Name, issueRequest);
            return existingIssues.Select(PolicyIssue.Create)
                                  .Where(pi => pi != null)
                                  .ToArray();
        }

        private static async Task UpdateIssuesAsync(GitHubClient client, RepoName policyRepo, ViolationReport report)
        {
            await CreateLabelsAsync(client, policyRepo, report.CreatedViolations);
            await CreateIssuesAsync(client, policyRepo, report.CreatedViolations);
            await ReopenIssuesAsync(client, policyRepo, report.ReopenedViolations);
            await CloseIssuesAsync(client, policyRepo, report.ClosedViolations);
        }

        private static async Task CreateLabelsAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            await client.PrintProgressAsync(Console.Out, "Loading label list");
            var existingLabels = await client.Issue.Labels.GetAllForRepository(policyRepo.Owner, policyRepo.Name);

            var existingLabelNames = existingLabels.ToDictionary(l => l.Name);
            var desiredLabelNames = violations.Select(v => v.Descriptor.DiagnosticId).Distinct().Concat(new[] { AreaViolationLabel, PolicyOverrideLabel }).ToArray();
            var missingLabelNames = desiredLabelNames.Where(di => !existingLabelNames.ContainsKey(di)).ToList();

            var descriptors = violations.Select(v => v.Descriptor).Distinct().ToDictionary(d => d.DiagnosticId);

            var i = 0;

            foreach (var missingLabelName in missingLabelNames)
            {
                await client.PrintProgressAsync(Console.Out, "Create label", missingLabelName, i++, missingLabelNames.Count);

                string color;
                string description;

                if (missingLabelName == AreaViolationLabel)
                {
                    color = "d4c5f9";
                    description = "Issues representing policy violations";
                }
                else if (missingLabelName == PolicyOverrideLabel)
                {
                    color = "3e820b";
                    description = "Marks an issue as a deliberate policy violation";
                }
                else
                {
                    var descriptor = descriptors[missingLabelName];
                    color = descriptor.Severity == PolicySeverity.Error
                        ? "e11d21"
                        : "fbca04";
                    description = descriptor.Title;
                }

                var newLabel = new NewLabel(missingLabelName, color)
                {
                    Description = description
                };
                await client.Issue.Labels.Create(policyRepo.Owner, policyRepo.Name, newLabel);
            }
        }

        private static async Task CreateIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations)
        {
            var allAssigness = violations.SelectMany(v => v.Assignees).ToHashSet();
            await GrantReadAccessAsync(client, policyRepo, allAssigness);

            var i = 0;

            foreach (var violation in violations)
            {
                await client.PrintProgressAsync(Console.Out, "Filing issue", violation.Title, i++, violations.Count);

                var title = $"{violation.Title} ({violation.Fingerprint})";
                var body = violation.Body;

                var newIssue = new NewIssue(title)
                {
                    Body = body,
                    Labels =
                    {
                        violation.Descriptor.DiagnosticId,
                        AreaViolationLabel
                    }
                };

                foreach (var assignee in violation.Assignees)
                    newIssue.Assignees.Add(assignee.Login);

            retry:
                try
                {
                    await client.Issue.Create(policyRepo.Owner, policyRepo.Name, newIssue);
                }
                catch (AbuseException ex)
                {
                    var retrySeconds = ex.RetryAfterSeconds ?? 120;
                    Console.WriteLine($"Abuse detection triggered. Waiting for {retrySeconds} seconds before retrying.");

                    var delay = TimeSpan.FromSeconds(retrySeconds);
                    await Task.Delay(delay);
                    goto retry;
                }
            }
        }

        private static async Task GrantReadAccessAsync(GitHubClient client, RepoName policyRepo, IReadOnlyCollection<CachedUser> users)
        {
            await client.PrintProgressAsync(Console.Out, $"Get collaborators for {policyRepo}...");
            var collaborators = await client.Repository.Collaborator.GetAll(policyRepo.Owner, policyRepo.Name);
            var collaboratorSet = collaborators.Select(c => c.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingUsers = users.Where(u => !collaboratorSet.Contains(u.Login)).ToArray();

            var i = 0;

            foreach (var user in missingUsers)
            {
                await client.PrintProgressAsync(Console.Out, "Granting pull", user.Login, i++, missingUsers.Length);
                var request = new CollaboratorRequest(Permission.Pull);
                await client.Repository.Collaborator.Add(policyRepo.Owner, policyRepo.Name, user.Login, request);
            }
        }

        private static async Task ReopenIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<(PolicyViolation, PolicyIssue)> violation)
        {
            var i = 0;

            foreach (var reopenedViolation in violation)
            {
                var issue = reopenedViolation.Item2.Issue;

                await client.PrintProgressAsync(Console.Out, "Reopening issue", issue.Title, i++, violation.Count);

                await client.Issue.Comment.Create(policyRepo.Owner, policyRepo.Name, issue.Number, "The violation still exists.");

                var issueUpdate = new IssueUpdate
                {
                    State = ItemState.Open
                };
                await client.Issue.Update(policyRepo.Owner, policyRepo.Name, issue.Number, issueUpdate);
            }
        }

        private static async Task CloseIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyIssue> issues)
        {
            var i = 0;

            foreach (var issue in issues)
            {
                var gitHubIssue = issue.Issue;

                await client.PrintProgressAsync(Console.Out, "Closing issue", gitHubIssue.Title, i++, issues.Count);

                await client.Issue.Comment.Create(policyRepo.Owner, policyRepo.Name, gitHubIssue.Number, "The violation was addressed.");

                var issueUpdate = new IssueUpdate
                {
                    State = ItemState.Closed
                };
                await client.Issue.Update(policyRepo.Owner, policyRepo.Name, gitHubIssue.Number, issueUpdate);
            }
        }

        private struct RepoName
        {
            public RepoName(string owner, string name)
            {
                Owner = owner;
                Name = name;
            }

            public string Owner { get; }
            public string Name { get; }

            public static bool TryParse(string ownerSlashName, out RepoName result)
            {
                result = default;

                if (string.IsNullOrEmpty(ownerSlashName))
                    return true;

                var slashPosition = ownerSlashName.IndexOf('/');
                var slashPositionLast = ownerSlashName.LastIndexOf('/');

                if (slashPosition != slashPositionLast)
                    return false;

                var owner = ownerSlashName.Substring(0, slashPosition).Trim();
                var name = ownerSlashName.Substring(slashPosition + 1).Trim();
                result = new RepoName(owner, name);
                return true;
            }

            public override string ToString()
            {
                return $"{Owner}/{Name}";
            }
        }

        private sealed class PolicyIssue
        {
            public PolicyIssue(Guid fingerprint, Issue issue)
            {
                Fingerprint = fingerprint;
                Issue = issue;
                IsOverride = issue.Labels.Any(l => l.Name == PolicyOverrideLabel);
            }

            public Guid Fingerprint { get; }
            public Issue Issue { get; }
            public bool IsOpen => Issue.State.Value == ItemState.Open;
            public bool IsClosed => !IsOpen;
            public bool IsOverride { get; }

            public static PolicyIssue Create(Issue issue)
            {
                var fingerprint = GetFingerprint(issue.Title);
                if (fingerprint == null)
                    return null;

                return new PolicyIssue(fingerprint.Value, issue);
            }

            private static Guid? GetFingerprint(string issueTitle)
            {
                var openParenthesis = issueTitle.LastIndexOf('(');
                var closeParenthesis = issueTitle.LastIndexOf(')');

                if (openParenthesis < 0 || closeParenthesis < 0 ||
                    openParenthesis >= closeParenthesis ||
                    closeParenthesis != issueTitle.Length - 1)
                {
                    return null;
                }

                var length = closeParenthesis - openParenthesis + 1;
                var text = issueTitle.Substring(openParenthesis + 1, length - 2);
                if (Guid.TryParse(text, out var result))
                    return result;

                return null;
            }
        }

        private sealed class ViolationReport
        {
            public ViolationReport(IReadOnlyList<(PolicyViolation v, PolicyIssue)> existingViolations, IReadOnlyList<(PolicyViolation Violation, PolicyIssue Issue)> overriddenViolations, IReadOnlyList<PolicyViolation> createdViolations, IReadOnlyList<(PolicyViolation, PolicyIssue)> reopenedViolations, IReadOnlyList<PolicyIssue> closedViolations)
            {
                ExistingViolations = existingViolations;
                OverriddenViolations = overriddenViolations;
                CreatedViolations = createdViolations;
                ReopenedViolations = reopenedViolations;
                ClosedViolations = closedViolations;
            }

            public IReadOnlyList<(PolicyViolation v, PolicyIssue)> ExistingViolations { get; }
            public IReadOnlyList<(PolicyViolation Violation, PolicyIssue Issue)> OverriddenViolations { get; }
            public IReadOnlyList<PolicyViolation> CreatedViolations { get; }
            public IReadOnlyList<(PolicyViolation, PolicyIssue)> ReopenedViolations { get; }
            public IReadOnlyList<PolicyIssue> ClosedViolations { get; }

            public IEnumerable<(string Status, PolicyViolation violation, PolicyIssue)> GetAll()
            {
                foreach (var (v, i) in ExistingViolations)
                    yield return ("Existing", v, i);

                foreach (var (v, i) in OverriddenViolations)
                    yield return ("Overridden", v, i);

                foreach (var v in CreatedViolations)
                    yield return ("New", v, null);

                foreach (var (v, i) in ReopenedViolations)
                    yield return ("Reopened", v, i);

                foreach (var i in ClosedViolations)
                    yield return ("Closed", null, i);
            }

            public static ViolationReport Create(IReadOnlyList<PolicyViolation> violations)
            {
                var existingViolations = Array.Empty<(PolicyViolation, PolicyIssue)>();
                var overriddenViolations = Array.Empty<(PolicyViolation, PolicyIssue)>();
                var createdViolations = violations;
                var reopenedViolations = Array.Empty<(PolicyViolation, PolicyIssue)>();
                var closedViolations = Array.Empty<PolicyIssue>();
                return new ViolationReport(existingViolations, overriddenViolations, createdViolations, reopenedViolations, closedViolations);
            }

            public static ViolationReport Create(IReadOnlyList<PolicyViolation> violations, IReadOnlyList<PolicyIssue> issues)
            {
                var violationByFingerprint = violations.ToDictionary(v => v.Fingerprint);

                var issueByFingerprint = issues.ToDictionary(i => i.Fingerprint);

                var mapping = new List<(PolicyViolation Violation, PolicyIssue Issue)>();

                foreach (var violation in violations)
                {
                    issueByFingerprint.TryGetValue(violation.Fingerprint, out var issue);
                    mapping.Add((violation, issue));
                }

                foreach (var issue in issues)
                {
                    if (!violationByFingerprint.ContainsKey(issue.Fingerprint))
                        mapping.Add((null, issue));
                }

                var existingViolations = mapping.Where(m => m.Violation != null && m.Issue != null)
                                                 .Where(m => m.Issue.IsOpen && !m.Issue.IsOverride)
                                                 .ToArray();

                var overriddenViolations = mapping.Where(m => m.Violation != null && m.Issue != null)
                                                   .Where(m => m.Issue.IsOverride)
                                                   .ToArray();

                var createdViolations = mapping.Where(m => m.Violation != null && m.Issue == null)
                                                .Select(m => m.Violation)
                                                .ToArray();

                var reopenedViolations = mapping.Where(m => m.Violation != null && m.Issue != null)
                                                 .Where(m => m.Issue.IsClosed && !m.Issue.IsOverride)
                                                 .ToArray();

                var closedViolations = mapping.Where(m => m.Issue != null)
                                               .Where(m => m.Issue.IsOpen)
                                               .Where(m => m.Violation == null || m.Issue.IsOverride)
                                               .Select(m => m.Issue)
                                               .ToArray();

                return new ViolationReport(existingViolations, overriddenViolations, createdViolations, reopenedViolations, closedViolations);
            }
        }
    }
}
