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
        public DateTimeOffset LastPush { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public CachedOrg Org { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetRepoUrl(Org.Name, Name);

        [JsonIgnore]
        public List<CachedTeamAccess> Teams { get; } = new List<CachedTeamAccess>();

        [JsonIgnore]
        public List<CachedUserAccess> Users { get; } = new List<CachedUserAccess>();
    }
}
