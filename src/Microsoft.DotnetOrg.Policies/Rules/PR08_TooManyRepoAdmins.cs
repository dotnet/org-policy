using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR08_TooManyRepoAdmins : PolicyRule
    {
        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 4;

            foreach (var repo in context.Org.Repos)
            {
                var numberOfAdmins = repo.Users.Count(ua => ua.Permission == CachedPermission.Admin &&
                                                           !ua.Describe().IsOwner);

                if (numberOfAdmins > Threshold)
                {
                    yield return new PolicyViolation(
                        "PR08",
                        title: $"Repo '{repo.Name}' has too many admins",
                        body: $@"
                            The repo {repo.Markdown()} has {numberOfAdmins} admins. Reduce the number of admins to {Threshold} or less.
                        ",
                        repo: repo
                    );
                }
            }
        }
    }
}
