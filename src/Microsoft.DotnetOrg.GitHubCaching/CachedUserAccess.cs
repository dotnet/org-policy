using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedUserAccess
    {
        public string RepoName { get; set; }
        public string UserLogin { get; set; }
        public CachedPermission Permission { get; set; }

        [JsonIgnore]
        public CachedOrg Org => Repo.Org;

        [JsonIgnore]
        public CachedRepo Repo { get; set; }

        [JsonIgnore]
        public CachedUser User { get; set; }

        public CachedAccessReason Describe()
        {
            foreach (var teamAccess in Repo.Teams)
            {
                if (teamAccess.Permission == Permission)
                {
                    foreach (var team in teamAccess.Team.DescendentsAndSelf())
                    {
                        if (team.Members.Contains(User))
                            return CachedAccessReason.FromTeam(team);
                    }
                }
            }

            return User.IsOwner
                    ? CachedAccessReason.FromOwner
                    : CachedAccessReason.FromCollaborator;
        }
    }
}
