using System.IO.Compression;
using System.Net.Http.Headers;
using Humanizer;
using Microsoft.DotnetOrg.GitHubCaching;
using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class CacheBuildCommand : ToolCommand
{
    private string? _orgName;
    private string? _buildId;

    public override string Name => "cache-build";

    public override string Description => "Caches the org from the latest build";

    public override void AddOptions(OptionSet options)
    {
        options.AddOrg(v => _orgName = v)
               .Add("build=", "The (optional) build {id} to use.", v => _buildId = v);
    }

    public override async Task ExecuteAsync()
    {
        var repos = new (string Org, string PolicyRepoOrg, string PolicyRepo)[]
        {
            ("aspnet", "aspnet", "org-policy-violations"),
            ("dotnet", "dotnet", "org-policy-violations"),
            ("nuget", "dotnet", "nuget-policy-violations"),
            ("mono", "dotnet", "mono-policy-violations"),
        };

        var client = await GitHubClientFactory.CreateAsync();

        foreach (var (org, policyRepoOrg, policyRepo) in repos)
        {
            try
            {
                var artifacts = await client.GetActionArtifacts(policyRepoOrg, policyRepo);
                var latest = artifacts.FirstOrDefault();
                if (latest is null)
                {
                    Console.WriteLine($"{org,-7} -- No results yet");
                }
                else
                {
                    var age = DateTimeOffset.UtcNow - latest.CreatedAt;
                    Console.WriteLine($"{org,-7} -- Caching build from {age.Humanize()} ago...");

                    var token = client.Credentials.Password;

                    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!;
                    var productHeader = new ProductInfoHeaderValue(new ProductHeaderValue(exeName));

                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
                    httpClient.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    await using var responseStream = await httpClient.GetStreamAsync(latest.ArchiveDownloadUrl);
                    await using var stream = new MemoryStream();
                    await responseStream.CopyToAsync(stream);
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    var entry = archive.Entries.FirstOrDefault(e => string.Equals(Path.GetExtension(e.Name), ".json", StringComparison.OrdinalIgnoreCase));
                    if (entry is not null)
                    {
                        var localFileName = CacheManager.GetOrgCache(org).FullName;
                        var directory = Path.GetDirectoryName(localFileName)!;
                        Directory.CreateDirectory(directory);

                        using var sourceStream = entry.Open();
                        using var targetStream = File.Create(localFileName);
                        await sourceStream.CopyToAsync(targetStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{org,-7} -- Can't cache results: {ex.Message}");
            }
        }
    }
}