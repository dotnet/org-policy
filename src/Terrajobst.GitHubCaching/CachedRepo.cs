
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Terrajobst.GitHubCaching
{
    public sealed class CachedRepo
    {
        public string Name { get; set; }
        public bool IsPrivate { get; set; }
        public DateTimeOffset LastPush { get; set; }

        [JsonIgnore]
        public List<CachedTeamAccess> Teams { get; } = new List<CachedTeamAccess>();

        [JsonIgnore]
        public List<CachedUserAccess> Users { get; } = new List<CachedUserAccess>();
    }
}
