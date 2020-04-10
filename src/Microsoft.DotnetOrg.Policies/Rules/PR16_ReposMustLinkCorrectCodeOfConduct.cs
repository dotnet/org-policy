using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public override async Task GetViolationsAsync(PolicyAnalysisContext context)
        {
            // TODO: Enable for other orgs
            //       mono, xamarin, xamarinhq
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

            var client = await GitHubClientFactory.CreateAsync();
            var problematicFiles = new HashSet<(string Name, string Url)>();

            foreach (var repo in context.Org.Repos)
            {
                if (repo.IsPrivate || repo.IsArchived)
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

                var coc = await client.GetCodeOfConduct(repo.Org.Name, repo.Name);
                if (coc != null)
                {
                    var containsExpectedLink = coc.Body.IndexOf(expectedLink, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!containsExpectedLink)
                        problematicFiles.Add((coc.Name, coc.Url));
                }

                void CheckForProblematicReferences(string name, string url, string contents)
                {
                    var containsProblematicReference = problematicReferences.Any(t => contents.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (containsProblematicReference)
                        problematicFiles.Add((name, url));
                }

                var readme = await client.GetReadme(repo.Org.Name, repo.Name);
                var contributing = await client.GetContributing(repo.Org.Name, repo.Name);

                if (coc != null)
                    CheckForProblematicReferences(coc.Name, coc.HtmlUrl, coc.Body);

                if (readme != null)
                    CheckForProblematicReferences(readme.Name, readme.HtmlUrl, readme.Content);

                if (contributing != null)
                    CheckForProblematicReferences(contributing.Name, contributing.HtmlUrl, contributing.Content);

                if (!problematicFiles.Any())
                    continue;

                var repoUrl = repo.Url;
                var linkedFileList = string.Join(", ", problematicFiles.OrderBy(t => t.Name).Select(f => $"[{f.Name}]({f.Url})"));

                context.ReportViolation(
                    Descriptor,
                    $"Repo '{repo.Name}' must link correct Code of Conduct",
                    $@"
                        The repo {repo.Markdown()} is a {kind} project. Thus, it should only reference the {kind} Code of Conduct.

                        For more details, see [PR15](https://github.com/dotnet/org-policy/blob/master/doc/PR15.md).

                        Affected files: {linkedFileList}
                    ",
                    repo: repo
                );
            }
        }
    }
}
