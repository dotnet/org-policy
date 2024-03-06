using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class CachedRepoEnvironment
{
    public long Id { get; set; }
    public string NodeId { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IReadOnlyList<CachedRepoSecret> Secrets { get; set; }

    [JsonIgnore]
    public CachedRepo Repo { get; set; }
}
#pragma warning restore CS8618