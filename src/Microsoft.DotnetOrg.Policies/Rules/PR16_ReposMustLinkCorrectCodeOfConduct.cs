using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR16_ReposMustLinkCorrectCodeOfConduct: PolicyRule
    {
        public override PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR16",
            "Repos must link correct Code of Conduct",
            PolicySeverity.Error
        );

        public override void GetViolations(PolicyAnalysisContext context)
        {
            // TODO: Enable for other orgs
            //       mono
            if (!string.Equals(context.Org.Name, "aspnet", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(context.Org.Name, "dotnet", StringComparison.OrdinalIgnoreCase))
                return;

            var probedFoundationCoCReferences = new[]
            {
                "http://dotnetfoundation.org/code-of-conduct",
                "https://dotnetfoundation.org/code-of-conduct"
            };

            var probedMicrosoftCocReferences = new[]
            {
                "http://opensource.microsoft.com/codeofconduct",
                "https://opensource.microsoft.com/codeofconduct",
                "opencode@microsoft.com"
            };

            var problematicFiles = new HashSet<(string Name, string Url)>();

            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsPrivate || repo.IsArchivedOrSoftArchived())
                    continue;

                // Let's also exclude forks and mirrors because adding a file to
                // these repos isn't always practical (as they usually belong to
                // other communities).
                if (repo.IsFork || repo.IsMirror)
                    continue;

                var kind = repo.IsUnderDotNetFoundation()
                    ? ".NET Foundation"
                    : "Microsoft";

                var expectedLink = repo.IsUnderDotNetFoundation()
                    ? CodeOfConduct.DotNetFoundationLink
                    : CodeOfConduct.MicrosoftLink;

                var problematicReferences = repo.IsUnderDotNetFoundation()
                    ? probedMicrosoftCocReferences
                    : probedFoundationCoCReferences;

                problematicFiles.Clear();

                // First check that the CoC links the expected CoC

                if (repo.CodeOfConduct is not null)
                {
                    var containsExpectedLink = repo.CodeOfConduct.Contents.Contains(expectedLink, StringComparison.OrdinalIgnoreCase);
                    if (!containsExpectedLink)
                        problematicFiles.Add((repo.CodeOfConduct.Name, repo.CodeOfConduct.Url));
                }

                void CheckForProblematicReferences(CachedFile? file)
                {
                    if (file is null)
                        return;

                    var containsProblematicReference = problematicReferences.Any(t => file.Contents.Contains(t, StringComparison.OrdinalIgnoreCase));
                    if (containsProblematicReference)
                        problematicFiles.Add((file.Name, file.Url));
                }

                CheckForProblematicReferences(repo.CodeOfConduct);
                CheckForProblematicReferences(repo.ReadMe);
                CheckForProblematicReferences(repo.Contributing);

                if (!problematicFiles.Any())
                    continue;

                var repoUrl = repo.Url;
                var linkedFileList = string.Join(", ", problematicFiles.OrderBy(t => t.Name).Select(f => $"[{f.Name}]({f.Url})"));

                context.ReportViolation(
                    Descriptor,
                    $"Repo '{repo.Name}' must link correct Code of Conduct",
                    $@"
                        The repo {repo.Markdown()} is a {kind} project. Thus, it should only reference the {kind} Code of Conduct.

                        For more details, see [PR15](https://github.com/dotnet/org-policy/blob/main/doc/PR15.md).

                        Affected files: {linkedFileList}
                    ",
                    repo: repo
                );
            }
        }
    }
}
