using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class CachedRepo
{
    public long Id { get; set; }
    public string Name { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsFork { get; set; }
    public bool IsMirror { get; set; }
    public DateTimeOffset LastPush { get; set; }
    public string Description { get; set; }
    public string DefaultBranchName { get; set; }
    public IReadOnlyList<CachedBranch> Branches { get; set; }
    public IReadOnlyList<CachedBranchProtectionRule> BranchProtectionRules { get; set; }
    public IReadOnlyList<CachedRepoEnvironment> Environments { get; set; }
    public IReadOnlyList<CachedRepoSecret> Secrets { get; set; }
    public IReadOnlyList<CachedRepoProperty> Properties { get; set; }
    public CachedFile? ReadMe { get; set; }
    public CachedFile? Contributing { get; set; }
    public CachedFile? CodeOfConduct { get; set; }
    public CachedFile? License { get; set; }
    public CachedActionPermissions ActionPermissions { get; set; }
    public IReadOnlyList<CachedFile> Workflows { get; set; }

    [JsonIgnore]
    public CachedOrg Org { get; set; }

    [JsonIgnore]
    public string Url => CachedOrg.GetRepoUrl(Org.Name, Name);

    [JsonIgnore]
    public string FullName => $"{Org.Name}/{Name}";

    [JsonIgnore]
    // Note: Repos that have never been pushed don't have a branch yet.
    public CachedBranch? DefaultBranch => Branches.SingleOrDefault(b => b.Name == DefaultBranchName);

    [JsonIgnore]
    public List<CachedTeamAccess> Teams { get; } = new List<CachedTeamAccess>();

    [JsonIgnore]
    public List<CachedUserAccess> Users { get; } = new List<CachedUserAccess>();

    [JsonIgnore]
    public List<CachedUserAccess> EffectiveUsers { get; } = new List<CachedUserAccess>();

    [JsonIgnore]
    public List<CachedOrgSecret> OrgSecrets { get; set; } = new List<CachedOrgSecret>();
}
#pragma warning restore CS8618