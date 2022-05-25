using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR21_MicrosoftOwnedReposShouldNotUseSecrets : PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR21",
            "Microsoft-owned repo should not use secrets",
            PolicySeverity.Hidden
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsArchivedOrSoftArchived())
                    continue;

                if (!repo.IsOwnedByMicrosoft())
                    continue;

                var secrets = repo.OrgSecrets.Cast<CachedSecret>()
                                             .Concat(repo.Secrets)
                                             .Concat(repo.Environments.SelectMany(e => e.Secrets))
                                             .Distinct();

                foreach (var secret in secrets)
                {
                    context.ReportViolation(
                        Descriptor,
                        $"Repo '{repo.Name}' should not use secret '{secret.Name}'",
                        $@"
                            The repo {repo.Markdown()} is a Microsoft-owned repo and thus shouldn't use secrets on GitHub. Rather, secret state should be kept in the internal Azure DevOps fork.
                        ",
                        repo: repo,
                        secret: secret
                    );
                }
            }
        }
    }
}
