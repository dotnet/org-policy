using System.Collections.Generic;
using System.Linq;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR11_ReposShouldHaveSufficientNumberOfAdmins : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR11",
            "Repos should have a sufficient number of admins",
            PolicySeverity.Warning
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 2;
            foreach (var repo in context.Org.Repos)
            {
                var numberOfAdmins = repo.Users.Count(ua => ua.Permission == CachedPermission.Admin &&
                                                            !ua.Describe().IsOwner);
                if (numberOfAdmins < Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Repo '{repo.Name}' needs more admins",
                        body: $@"
                            The repo {repo.Markdown()} has {numberOfAdmins} admins (excluding organization owners). It is recommended to have at least {Threshold} admins.
                        ",
                        repo: repo
                    );
                }
            }
        }
    }
}
