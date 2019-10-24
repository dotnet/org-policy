using System.Text.Json.Serialization;

namespace GitHubPermissionSurveyor
{
    internal class CachedUserAccess
    {
        public string RepoName { get; set; }
        public string User { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedRepo Repo { get; set; }
    }
}
