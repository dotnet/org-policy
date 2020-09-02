using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedRepo
    {
        public string Name { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsArchived { get; set; }
        public bool IsTemplate { get; set; }
        public bool IsFork { get; set; }
        public bool IsMirror { get; set; }
        public DateTimeOffset LastPush { get; set; }
        public string Description { get; set; }
        public string DefaultBranch { get; set; }
        public IReadOnlyList<string> Branches { get; set; }

        [JsonIgnore]
        public CachedOrg Org { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetRepoUrl(Org.Name, Name);

        [JsonIgnore]
        public string FullName => $"{Org.Name}/{Name}";

        [JsonIgnore]
        public List<CachedTeamAccess> Teams { get; } = new List<CachedTeamAccess>();

        [JsonIgnore]
        public List<CachedUserAccess> Users { get; } = new List<CachedUserAccess>();

        [JsonIgnore]
        public List<CachedUserAccess> EffectiveUsers { get; } = new List<CachedUserAccess>();
    }
}
