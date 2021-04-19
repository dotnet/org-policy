using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // This is a serialized type.
    public sealed class CachedBranch
    {
        public string Prefix { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }

        [JsonIgnore]
        public CachedOrg Org => Repo?.Org!;

        [JsonIgnore]
        public CachedRepo Repo { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetBranchUrl(Org.Name, Repo.Name, Name);

        public override string ToString()
        {
            return Name;
        }
    }
#pragma warning restore CS8618
}
