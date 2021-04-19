using System;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // Serialized type
    public class CachedRepoSecret : CachedSecret
    {
        [JsonIgnore]
        public CachedRepo Repo { get; set; }

        [JsonIgnore]
        public CachedRepoEnvironment? Environment { get; set; }
    }
#pragma warning restore CS8618
}
