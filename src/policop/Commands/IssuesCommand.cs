using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class IssuesCommand : ToolCommand
    {
        private string? _orgName;
        private string? _repoName;
        private readonly List<string> _labels = new List<string>();
        private bool _viewInExcel;

        public override string Name => "issues";

        public override string Description => "Shows the list of open issues";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r=", "Specifies the repo", v => _repoName = v)
                   .Add("l=", "Specifies the label", v => _labels.Add(v))
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true);
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (string.IsNullOrEmpty(_repoName))
            {
                Console.Error.WriteLine($"error: -r must be specified");
                return;
            }

            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var request = new RepositoryIssueRequest();
            request.Filter = IssueFilter.All;
            request.State = ItemStateFilter.Open;

            foreach (var label in _labels)
                request.Labels.Add(label);

            var issues = await client.Issue.GetAllForRepository(_orgName, _repoName, request);

            var document = new CsvDocument("Id", "Link", "Title", "Labels");

            using (var writer = document.Append())
            {
                foreach (var issue in issues)
                {
                    var labelList = string.Join(", ", issue.Labels.Select(l => l.Name));

                    writer.Write($"{_orgName}/{_repoName}#{issue.Number}");
                    writer.Write($"{issue.HtmlUrl}");
                    writer.Write($"{issue.Title}");
                    writer.Write($"{labelList}");
                    writer.WriteLine();
                }
            }

            if (_viewInExcel)
                document.ViewInExcel();
            else
                document.PrintToConsole();
        }
    }
}
