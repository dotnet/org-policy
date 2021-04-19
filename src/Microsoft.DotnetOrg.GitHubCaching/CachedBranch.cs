using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // This is a serialized type.
    public sealed class CachedBranch
    {
        [JsonIgnore]
        public string Ref => $"{Prefix}/{Name}";

        public string Prefix { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }

        [JsonIgnore]
        public CachedOrg Org => Repo?.Org!;

        [JsonIgnore]
        public CachedRepo Repo { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetBranchUrl(Org.Name, Repo.Name, Name);

        [JsonIgnore]
        public IEnumerable<CachedBranchProtectionRule> Rules => Repo.BranchProtectionRules.Where(r => r.MatchingRefs.Contains(Ref));

        public override string ToString()
        {
            return Name;
        }
    }
#pragma warning restore CS8618
}
