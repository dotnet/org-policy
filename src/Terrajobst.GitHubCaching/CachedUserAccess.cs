using System.Text.Json.Serialization;

namespace Terrajobst.GitHubCaching
{
    public sealed class CachedUserAccess
    {
        public string RepoName { get; set; }
        public string User { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedRepo Repo { get; set; }
    }
}
