using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR08_TooManyRepoAdmins : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR08",
            "Too many repo admins",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 10;

            foreach (var repo in context.Org.Repos)
            {
                // Note: Even if we don't fallback to owners, we don't want owners to
                //       to count towards the admin quota.
                var numberOfAdmins = repo.GetAdministrators(fallbackToOwners: false).Count(u => !u.IsOwner);

                if (numberOfAdmins > Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Repo '{repo.Name}' has too many admins",
                        body: $@"
                            The repo {repo.Markdown()} has {numberOfAdmins} admins. Reduce the number of admins to {Threshold} or less.
                        ",
                        org: context.Org,
                        repo: repo
                    );
                }
            }
        }
    }
}
