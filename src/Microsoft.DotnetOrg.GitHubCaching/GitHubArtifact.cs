using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching;

public sealed class GitHubArtifact
{
    public GitHubArtifact(long id,
                          string nodeId,
                          string name,
                          long sizeInBytes,
                          string url,
                          string archiveDownloadUrl,
                          bool expired,
                          DateTimeOffset createdAt,
                          DateTimeOffset updatedAt,
                          DateTimeOffset expiresAt)
    {
        Id = id;
        NodeId = nodeId;
        Name = name;
        SizeInBytes = sizeInBytes;
        Url = url;
        ArchiveDownloadUrl = archiveDownloadUrl;
        Expired = expired;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ExpiresAt = expiresAt;
    }

    [JsonPropertyName("id")]
    public long Id { get; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("size_in_bytes")]
    public long SizeInBytes { get; }

    [JsonPropertyName("url")]
    public string Url { get; }

    [JsonPropertyName("archive_download_url")]
    public string ArchiveDownloadUrl { get; }

    [JsonPropertyName("expired")]
    public bool Expired { get; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; }
}