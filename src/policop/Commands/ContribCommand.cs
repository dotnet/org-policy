using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;
using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ContribCommand : ToolCommand
    {
        private string _orgName;
        private string _repoName;
        private List<string> _refs = new List<string>();
        private bool _viewInExcel;
        private string _since;
        private bool _nonMicrosoftOnly;
        private bool _markdown;

        public override string Name => "contrib";

        public override string Description => "Produces a report of all contributors";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r=", "Specifies the repo", v => _repoName = v)
                   .Add("non-microsoft-only", "Only shows non-Microsoft folks", v => _nonMicrosoftOnly = true)
                   .Add("markdown", "Renders result as Markdown", v => _markdown = true)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("since=", "Show contributions since a particular point in time", v => _since = v)
                   .Add("<>", v => _refs.Add(v));
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

            if (_markdown && _viewInExcel)
            {
                Console.Error.WriteLine("error: can only specify --markdown or --excel");
                return;
            }

            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            DateTime? since = null;

            if (_since is not null)
            {
                if (!DateTime.TryParse(_since, out var s))
                {
                    Console.Error.WriteLine("error: --since must be a valid date");
                    return;
                }

                since = s;
            }

            if (since is not null)
            {
                if (_refs.Count != 1)
                {
                    Console.Error.WriteLine("error: need a single argument with the ref to compare to");
                    return;
                }
            }
            else
            {
                if (_refs.Count != 2)
                {
                    Console.Error.WriteLine("error: need two arguments with the refs to compare against each other");
                    return;
                }
            }

            var client = await GitHubClientFactory.CreateAsync();

            IReadOnlyList<GitHubCommit> commits;

            if (since is not null)
            {
                var commitRequest = new CommitRequest
                {
                    Sha = _refs[0],
                    Since = since
                };
                Console.WriteLine($"Looking for commits in {commitRequest.Sha} since {commitRequest.Since}...");
                commits = await client.Repository.Commit.GetAll(_orgName, _repoName, commitRequest);
            }
            else
            {
                Console.WriteLine($"Looking for commits between {_refs[0]} and {_refs[1]}...");
                var result = await client.Repository.Commit.Compare(_orgName, _repoName, _refs[0], _refs[1]);
                commits = result.Commits;
            }

            Console.WriteLine($"Found {commits.Count} commits. Filtering out commits by Microsoft employees...");

            var ospoClient = await OspoClientFactory.CreateAsync();
            var allMicrosofties = await ospoClient.GetAllAsync();

            var report = (from c in commits
                          where c.Author?.Login is not null && !IsBot(c.Author.Login)
                          group c by c.Author.Login into g
                          select (Login: g.Key, Commits: g.Count(), Link: allMicrosofties.LinkByLogin.GetValueOrDefault(g.Key)))
                          .OrderByDescending(r => r.Commits)
                          .ThenBy(r => r.Login)
                          .ToArray();

            if (_markdown)
            {
                foreach (var row in report)
                {
                    if (IsMicrosoft(row.Login, row.Link) && _nonMicrosoftOnly)
                        continue;

                    Console.WriteLine($"[{row.Login} ({row.Commits})](https://github.com/{_orgName}/{_repoName}/commits/{_refs[1]}?author={row.Login})");
                }
            }
            else
            {
                var document = new CsvDocument("User", "Commits", "IsMicrosoft");

                using (var writer = document.Append())
                {
                    foreach (var row in report)
                    {
                        var isMicrosoft = IsMicrosoft(row.Login, row.Link);
                        if (isMicrosoft && _nonMicrosoftOnly)
                            continue;

                        writer.Write(row.Login);
                        writer.Write(row.Commits.ToString());
                        writer.Write(isMicrosoft ? "Yes" : "No");
                        writer.WriteLine();
                    }
                }

                if (_viewInExcel)
                    document.ViewInExcel();
                else
                    document.PrintToConsole();
            }
        }

        private bool IsBot(string login)
        {
            return login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMicrosoft(string login, OspoLink link)
        {
            // We could use the APIs to figure out whether this user is a known bot. However,
            // We don't have many bots that commit, so manual exclusion is easier than requiring
            // callers of this script to have permissions/auth keys.

            if (string.Equals(login, "dotnet-maestro-bot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(login, "dotnet-maestro[bot]", StringComparison.OrdinalIgnoreCase))
                return true;

            return link is not null && link.MicrosoftInfo is not null;
        }
    }
}
