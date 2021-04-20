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

        [JsonIgnore]
        public override string Url
        {
            get
            {
                if (Environment is not null)
                    return $"https://github.com/{Repo.Org.Name}/{Repo.Name}/settings/environments/{Environment.Id}/edit";
                else
                    return $"https://github.com/{Repo.Org.Name}/{Repo.Name}/settings/secrets/actions";
            }
        }
    }
#pragma warning restore CS8618
}
