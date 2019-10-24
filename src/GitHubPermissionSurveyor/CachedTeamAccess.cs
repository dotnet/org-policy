using System.Text.Json.Serialization;

namespace GitHubPermissionSurveyor
{
    internal class CachedTeamAccess
    {
        public string RepoName { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedRepo Repo { get; internal set; }

        [JsonIgnore]
        public CachedTeam Team { get; set; }
    }
}
