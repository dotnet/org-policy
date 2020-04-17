using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class StatusCheckCommand : ToolCommand
    {
        private string _orgName;
        private string _repoName;
        private string _branchName = "*";
        private string _statusCheck;
        private bool _disable;

        public override string Name => "status-check";

        public override string Description => "Shows or disables a given status check";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("r=", "Specifies the {name} of the repo", v => _repoName = v)
                   .Add("branch=", "Specifies the {name} of the branch", v => _branchName = v)
                   .Add("check=", "Specifies the {name} of the status check", v => _statusCheck = v)
                   .Add("disable", "Disables the given status check", v => _disable = true);
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

            if (string.IsNullOrEmpty(_statusCheck))
            {
                Console.Error.WriteLine($"error: --check must be specified");
                return;
            }

            var org = await CacheManager.LoadOrgAsync(_orgName);

            if (org == null)
            {
                Console.Error.WriteLine($"error: org '{_orgName}' not cached yet. Run cache-build or cache-org first.");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var matchingRepos = org.Repos.OrderBy(r => r.Name)
                                         .Where(r => _repoName == "*" || string.Equals(r.Name, _repoName, StringComparison.OrdinalIgnoreCase))
                                         .ToArray();

            if (matchingRepos.Length == 0)
            {
                Console.WriteLine($"warning: no repos found");
            }
            else
            {
                foreach (var repo in matchingRepos)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"Checking {repo.FullName}...");
                    Console.ResetColor();

                    var branches = await client.Repository.Branch.GetAll(repo.Org.Name, repo.Name);
                    var matchinBranches = branches.Where(b => b.Protected)
                                                  .Where(b => _branchName == "*" || string.Equals(b.Name, _branchName, StringComparison.Ordinal))
                                                  .ToArray();

                    foreach (var branch in matchinBranches)
                    {
                        var protectionSettings = await client.Repository.Branch.GetBranchProtection(repo.Org.Name, repo.Name, branch.Name);

                        if (protectionSettings?.RequiredStatusChecks != null)
                        {
                            var contexts = protectionSettings.RequiredStatusChecks.Contexts.ToList();
                            var matchingContexts = contexts.Where(c => _statusCheck == "*" || string.Equals(c, _statusCheck, StringComparison.OrdinalIgnoreCase))
                                                           .ToArray();

                            foreach (var context in matchingContexts)
                            {
                                Console.Write($"{repo.FullName} @ {branch.Name} {context} ");

                                if (!_disable)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[ENABLED]");
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[DISABLED]");
                                }

                                Console.ResetColor();
                            }

                            if (_disable && matchingContexts.Any())
                            {
                                foreach (var context in matchingContexts)
                                    contexts.Remove(context);


                                contexts.RemoveAll(c => string.Equals(c, _statusCheck, StringComparison.OrdinalIgnoreCase));
                                await client.Repository.Branch.UpdateRequiredStatusChecksContexts(repo.Org.Name, repo.Name, branch.Name, contexts);
                            }
                        }
                    }
                }
            }
        }
    }
}
