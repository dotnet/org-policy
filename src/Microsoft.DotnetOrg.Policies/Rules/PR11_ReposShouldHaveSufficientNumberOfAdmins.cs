using System.Linq;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR11_ReposShouldHaveSufficientNumberOfAdmins : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR11",
            "Repos should have a sufficient number of admins",
            PolicySeverity.Warning
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            const int Threshold = 2;
            foreach (var repo in context.Org.Repos)
            {
                if (!repo.IsOwnedByMicrosoft())
                    continue;

                var isArchived = repo.IsArchived;
                var numberOfAdmins = repo.GetAdministrators(fallbackToOwners: false).Count();

                if (!isArchived && numberOfAdmins < Threshold)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Repo '{repo.Name}' needs more admins",
                        $@"
                            The repo {repo.Markdown()} has {numberOfAdmins} admins (excluding organization owners). It is recommended to have at least {Threshold} admins.
                        ",
                        repo: repo
                    );
                }
            }
        }
    }
}
