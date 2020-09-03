using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR08_TooManyRepoAdmins : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR08",
            "Too many repo admins",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 10;

            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsArchived)
                    continue;

                // Note: Even if we don't fallback to owners, we don't want owners to
                //       to count towards the admin quota.
                var numberOfAdmins = repo.GetAdministrators(fallbackToOwners: false)
                                          .Count(u => !u.IsOwner && !u.IsBot());

                if (numberOfAdmins > Threshold)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Repo '{repo.Name}' has too many admins",
                        $@"
                            The repo {repo.Markdown()} has {numberOfAdmins} admins. Reduce the number of admins to {Threshold} or less.
                        ",
                        repo: repo
                    );
                }
            }
        }
    }
}
