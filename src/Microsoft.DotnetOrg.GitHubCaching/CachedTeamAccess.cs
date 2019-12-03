using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public class CachedTeamAccess
    {
        public string RepoName { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedOrg Org => Repo.Org;

        [JsonIgnore]
        public string TeamSlug { get; set; }

        [JsonIgnore]
        public CachedRepo Repo { get; internal set; }

        [JsonIgnore]
        public CachedTeam Team { get; set; }
    }
}
