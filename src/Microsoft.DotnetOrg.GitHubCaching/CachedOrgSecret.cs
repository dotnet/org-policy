using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // Serialized type
    public sealed class CachedOrgSecret : CachedSecret
    {
        public string Visibility { get; set; }
        public IReadOnlyList<string> RepositoryNames { get; set; }

        [JsonIgnore]
        public CachedOrg Org { get; set; }

        [JsonIgnore]
        public IReadOnlyList<CachedRepo> Repositories { get; set; }
    }
#pragma warning restore CS8618
}
