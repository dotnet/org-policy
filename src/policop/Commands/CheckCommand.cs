using System.Diagnostics;
using System.Text;
using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Policies;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class CheckCommand : ToolCommand
{
    private string? _orgName;
    private string? _outputFileName;
    private string? _policyRepo;
    private bool _updateIssues;
    private bool _assignIssues;
    private bool _viewInExcel;
    private string? _writeIssuesTo;
    private SortedSet<string> _includedRuleIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    private ICollection<string>? _activeTerms;

    public override string Name => "check";

    public override string Description => "Checks for policy violations and optionally files issues";

    public override void AddOptions(OptionSet options)
    {
        options.AddOrg(v => _orgName = v)
               .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
               .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
               .Add("policy-repo=", "The GitHub {repo} policy violations should be filed in.", v => _policyRepo = v)
               .Add("update-issues", "Will create, repopen and closed policy violations.", v => _updateIssues = true)
               .Add("assign-issues", "If specified, it will assign issues as well", v => _assignIssues = true)
               .Add("write-issues-to=", "The {path} to a directory in which to save new issues", v => _writeIssuesTo = v)
               .Add("r|rule", "Include rule {id}", v => _activeTerms = _includedRuleIds)
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

        if (org is null)
        {
            Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-build or cache-org first.");
            return;
        }

        if (_updateIssues && string.IsNullOrEmpty(_policyRepo))
        {
            Console.Error.WriteLine($"error: --policy-repo must be specified if --update-issues is specified.");
            return;
        }

        if (_assignIssues && !_updateIssues)
        {
            Console.Error.WriteLine($"error: --assign-issues is only valid if --update-issues is specified too.");
            return;
        }

        if (!RepoName.TryParse(_policyRepo, out var policyRepo))
        {
            Console.Error.WriteLine($"error: policy repo must be of form owner/name but was '{_policyRepo}'.");
            return;
        }

        var gitHubClient = string.IsNullOrEmpty(_policyRepo)
            ? null
            : await GitHubClientFactory.CreateAsync();

        var includedRules = PolicyRunner.GetRules().ToList();
        var existingRules = includedRules.Select(r => r.Descriptor.DiagnosticId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_includedRuleIds.Count == 0)
        {
            // If we weren't given a list of rules, we only include rules
            // that aren't hidden.
            includedRules.RemoveAll(r => r.Descriptor.Severity <= PolicySeverity.Hidden);
        }
        else
        {
            // Otherwise we include the rules specified, regardless of their
            // severity.
            var invalidRuleIds = _includedRuleIds.Where(r => !existingRules.Contains(r));
            foreach (var invalidRuleId in invalidRuleIds)
                Console.Error.WriteLine($"warning: policy rule '{invalidRuleId}' doesn't exist");

            includedRules.RemoveAll(r => !_includedRuleIds.Contains(r.Descriptor.DiagnosticId));
        }

        var stopwatch = Stopwatch.StartNew();

        var context = new PolicyAnalysisContext(org);
        await PolicyRunner.RunAsync(context, includedRules);
        var violations = context.GetViolations();

        stopwatch.Stop();

        var report = gitHubClient is null
            ? ViolationReport.Create(violations)
            : await CreateViolationReportAsync(gitHubClient, policyRepo, violations);

        SaveVioloations(_orgName, _outputFileName, _viewInExcel, report);

        if (_writeIssuesTo is not null)
            WriteIssues(_writeIssuesTo, report);

        if (_updateIssues)
        {
            Debug.Assert(gitHubClient is not null);
            await UpdateIssuesAsync(gitHubClient, policyRepo, report, _assignIssues);
        }

        Console.WriteLine($"  Existing violations: {report.ExistingViolations.Count:N0}");
        Console.WriteLine($"Overridden violations: {report.OverriddenViolations.Count:N0}");
        Console.WriteLine($"   Created violations: {report.CreatedViolations.Count:N0}");
        Console.WriteLine($"  Reopened violations: {report.ReopenedViolations.Count:N0}");
        Console.WriteLine($"    Closed violations: {report.ClosedViolations.Count:N0}");
        Console.WriteLine($"             Duration: {stopwatch.Elapsed}");
    }

    private static readonly string AreaViolationLabel = "area-violation";
    private static readonly string PolicyOverrideLabel = "policy-override";

    private static void SaveVioloations(string orgName, string? outputFileName, bool viewInExcel, ViolationReport report)
    {
        var violations = report.GetAll()
            .Where(r => r.Violation is not null)
            .Select(r => r.Violation!);
        var hasAnyRepos = violations.Any(v => v.Repo is not null);
        var hasAnySecrets = violations.Any(v => v.Secret is not null);
        var hasAnyBranches = violations.Any(v => v.Branch is not null);
        var hasAnyUsers = violations.Any(v => v.User is not null);
        var hasAnyTeams = violations.Any(v => v.Team is not null);

        var headers = new List<string>
        {
            "fingerprint",
            "org",
            "status",
            "severity",
            "rule",
            "rule-title",
            "violation",
            "repo",
            "secret",
            "branch",
            "user",
            "team",
            "assignees",
            "emails"
        };

        if (!hasAnyRepos)
            headers.Remove("repo");

        if (!hasAnySecrets)
            headers.Remove("secret");

        if (!hasAnyBranches)
            headers.Remove("branch");

        if (!hasAnyUsers)
            headers.Remove("user");

        if (!hasAnyTeams)
            headers.Remove("team");

        var document = new CsvDocument(headers);

        using (var writer = document.Append())
        {
            foreach (var (status, violation, _) in report.GetAll())
            {
                if (violation is null)
                    continue;

                writer.Write(violation.Fingerprint.ToString());
                writer.Write(orgName);
                writer.Write(status);
                writer.Write(violation.Descriptor.Severity.ToString());
                writer.Write(violation.Descriptor.DiagnosticId);
                writer.Write(violation.Descriptor.Title);
                writer.Write(violation.Title);

                if (hasAnyRepos)
                {
                    if (violation.Repo is null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Repo.Url, violation.Repo.Name, viewInExcel);
                }

                if (hasAnySecrets)
                {
                    if (violation.Secret is null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Secret.Url, violation.Secret.Name, viewInExcel);
                }

                if (hasAnyBranches)
                {
                    if (violation.Branch is null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Branch.Url, violation.Branch.Name, viewInExcel);
                }

                if (hasAnyUsers)
                {
                    if (violation.User is null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.User.Url, violation.User.Login, viewInExcel);
                }

                if (hasAnyTeams)
                {
                    if (violation.Team is null)
                        writer.Write(string.Empty);
                    else
                        writer.WriteHyperlink(violation.Team.Url, violation.Team.Name, viewInExcel);
                }

                var assignees = string.Join(", ", violation.Assignees.Select(r => r.Login));
                writer.Write(assignees);

                var emails = string.Join(", ", violation.Assignees.Select(a => a.GetEmailName())
                    .Where(e => !string.IsNullOrEmpty(e)));
                writer.Write(emails);

                writer.WriteLine();
            }
        }

        if (!string.IsNullOrEmpty(outputFileName))
            document.Save(outputFileName);

        if (viewInExcel)
            document.ViewInExcel();
    }

    private void WriteIssues(string directoryPath, ViolationReport report)
    {
        Directory.CreateDirectory(directoryPath);

        foreach (var violation in report.CreatedViolations)
        {
            var sb = new StringBuilder();
            sb.Append("# ");
            sb.Append(violation.Title);
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(violation.Body);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("## Assignees");
            sb.AppendLine();
            foreach (var user in violation.Assignees)
            {
                sb.Append("* ");
                sb.Append(user.Markdown());
                sb.AppendLine();
            }

            var fileName = $"{violation.DiagnosticId} {violation.Title} ({violation.Fingerprint}).md";
            var path = Path.Combine(directoryPath, fileName);
            File.WriteAllText(path, sb.ToString());
        }
    }

    private static async Task<ViolationReport> CreateViolationReportAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations)
    {
        var existingIssues = await GetIssuesAsync(client, policyRepo);
        return ViolationReport.Create(violations, existingIssues);
    }

    private static async Task<IReadOnlyList<PolicyIssue>> GetIssuesAsync(GitHubClient client, RepoName policyRepo)
    {
        Console.WriteLine("Loading issue list");
        var issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All
        };
        issueRequest.Labels.Add(AreaViolationLabel);
        var existingIssues = await client.InvokeAsync(c => c.Issue.GetAllForRepository(policyRepo.Owner, policyRepo.Name, issueRequest));
        return existingIssues.Select(PolicyIssue.Create)
            .Where(pi => pi is not null)
            .Select(pi => pi!)
            .ToArray();
    }

    private static async Task UpdateIssuesAsync(GitHubClient client, RepoName policyRepo, ViolationReport report, bool assignIssues)
    {
        await CreateLabelsAsync(client, policyRepo, report.CreatedViolations);
        await CreateIssuesAsync(client, policyRepo, report.CreatedViolations, assignIssues);
        await ReopenIssuesAsync(client, policyRepo, report.ReopenedViolations);
        await UpdateIssuesAsync(client, policyRepo, report);
        await CloseIssuesAsync(client, policyRepo, report.ClosedViolations);
    }

    private static async Task CreateLabelsAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations)
    {
        Console.WriteLine("Loading label list");
        var existingLabels = await client.InvokeAsync(c => c.Issue.Labels.GetAllForRepository(policyRepo.Owner, policyRepo.Name));

        var existingLabelNames = existingLabels.ToDictionary(l => l.Name);
        var desiredLabelNames = violations.Select(v => v.Descriptor.DiagnosticId).Distinct().Concat(new[] { AreaViolationLabel, PolicyOverrideLabel }).ToArray();
        var missingLabelNames = desiredLabelNames.Where(di => !existingLabelNames.ContainsKey(di)).ToList();

        var descriptors = violations.Select(v => v.Descriptor).Distinct().ToDictionary(d => d.DiagnosticId);

        foreach (var missingLabelName in missingLabelNames)
        {
            Console.WriteLine($"Creating label '{missingLabelName}'...");

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
            await client.InvokeAsync(c => c.Issue.Labels.Create(policyRepo.Owner, policyRepo.Name, newLabel));
        }
    }

    private static async Task CreateIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyViolation> violations, bool assignIssues)
    {
        if (assignIssues)
        {
            var allAssigness = violations.SelectMany(v => v.Assignees).ToHashSet();
            await GrantReadAccessAsync(client, policyRepo, allAssigness);
        }

        foreach (var violation in violations)
        {
            Console.WriteLine($"Filing issue '{violation.Title}'...");

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

            if (assignIssues)
            {
                foreach (var assignee in violation.Assignees)
                    newIssue.Assignees.Add(assignee.Login);
            }

            await client.InvokeAsync(c => c.Issue.Create(policyRepo.Owner, policyRepo.Name, newIssue));
        }
    }

    private static async Task GrantReadAccessAsync(GitHubClient client, RepoName policyRepo, IReadOnlyCollection<CachedUser> users)
    {
        Console.WriteLine($"Get collaborators for {policyRepo}...");

        var collaborators = await client.InvokeAsync(c => c.Repository.Collaborator.GetAll(policyRepo.Owner, policyRepo.Name));
        var collaboratorSet = collaborators.Select(c => c.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingUsers = users.Where(u => !collaboratorSet.Contains(u.Login)).ToArray();

        foreach (var user in missingUsers)
        {
            Console.WriteLine($"Granting pull to '{user.Login}'...");
            var request = new CollaboratorRequest(Permission.Pull);
            await client.InvokeAsync(c => c.Repository.Collaborator.Add(policyRepo.Owner, policyRepo.Name, user.Login, request));
        }
    }

    private static async Task ReopenIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<(PolicyViolation, PolicyIssue)> violations)
    {
        foreach (var reopenedViolation in violations)
        {
            var issue = reopenedViolation.Item2.Issue;

            Console.WriteLine($"Reopening issue '{issue.Title}'...");

            await client.InvokeAsync(c => c.Issue.Comment.Create(policyRepo.Owner, policyRepo.Name, issue.Number, "The violation still exists."));

            var issueUpdate = new IssueUpdate
            {
                State = ItemState.Open
            };

            await client.InvokeAsync(c => c.Issue.Update(policyRepo.Owner, policyRepo.Name, issue.Number, issueUpdate));
        }
    }

    private static async Task UpdateIssuesAsync(GitHubClient client, RepoName policyRepo, ViolationReport report)
    {
        var updatedIssues = report.ExistingViolations.Concat(report.ReopenedViolations)
            .Where(t => t.Item1.Body != t.Item2.Issue.Body)
            .ToArray();

        foreach (var violation in updatedIssues)
        {
            var newBody = violation.Item1.Body;
            var issue = violation.Item2.Issue;

            Console.WriteLine($"Updating issue body '{issue.Title}'...");

            // Don't update titles or people will get annoyed because it breaks Outlook threading.

            var issueUpdate = new IssueUpdate
            {
                Body = newBody
            };

            await client.InvokeAsync(c => c.Issue.Update(policyRepo.Owner, policyRepo.Name, issue.Number, issueUpdate));
        }
    }

    private static async Task CloseIssuesAsync(GitHubClient client, RepoName policyRepo, IReadOnlyList<PolicyIssue> issues)
    {
        foreach (var issue in issues)
        {
            var gitHubIssue = issue.Issue;

            Console.WriteLine($"Closing issue '{gitHubIssue.Title}'...");

            await client.InvokeAsync(c => c.Issue.Comment.Create(policyRepo.Owner, policyRepo.Name, gitHubIssue.Number, "The violation was addressed."));

            var issueUpdate = new IssueUpdate
            {
                State = ItemState.Closed
            };

            await client.InvokeAsync(c => c.Issue.Update(policyRepo.Owner, policyRepo.Name, gitHubIssue.Number, issueUpdate));
        }
    }

    private readonly struct RepoName
    {
        public RepoName(string owner, string name)
        {
            Owner = owner;
            Name = name;
        }

        public string Owner { get; }
        public string Name { get; }

        public static bool TryParse(string? ownerSlashName, out RepoName result)
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

        public static PolicyIssue? Create(Issue issue)
        {
            var fingerprint = GetFingerprint(issue.Title);
            if (fingerprint is null)
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

        public IEnumerable<(string Status, PolicyViolation? Violation, PolicyIssue? Issue)> GetAll()
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

            var mapping = new List<(PolicyViolation? Violation, PolicyIssue? Issue)>();

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

            var existingViolations = mapping.Where(m => m.Violation is not null && m.Issue is not null)
                .Select(m => (Violation: m.Violation!, Issue: m.Issue!))
                .Where(m => m.Issue.IsOpen && !m.Issue.IsOverride)
                .ToArray();

            var overriddenViolations = mapping.Where(m => m.Violation is not null && m.Issue is not null)
                .Select(m => (Violation: m.Violation!, Issue: m.Issue!))
                .Where(m => m.Issue.IsOverride)
                .ToArray();

            var createdViolations = mapping.Where(m => m.Violation is not null && m.Issue is null)
                .Select(m => m.Violation!)
                .ToArray();

            var reopenedViolations = mapping.Where(m => m.Violation is not null && m.Issue is not null)
                .Select(m => (Violation: m.Violation!, Issue: m.Issue!))
                .Where(m => m.Issue.IsClosed && !m.Issue.IsOverride)
                .ToArray();

            var closedViolations = mapping.Where(m => m.Issue is not null && m.Issue.IsOpen)
                .Select(m => (m.Violation, Issue: m.Issue!))
                .Where(m => m.Violation is null || m.Issue.IsOverride)
                .Select(m => m.Issue)
                .ToArray();

            return new ViolationReport(existingViolations, overriddenViolations, createdViolations, reopenedViolations, closedViolations);
        }
    }
}