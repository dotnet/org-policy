using System.Collections.Generic;
using System.Linq;

namespace Terrajobst.GitHubCaching
{
    public sealed class CachedOrg
    {
        public string Name { get; set; }
        public List<string> Owners { get; set; } = new List<string>();
        public List<CachedTeam> Teams { get; set; } = new List<CachedTeam>();
        public List<CachedRepo> Repos { get; set; } = new List<CachedRepo>();
        public List<CachedUserAccess> Collaborators { get; set; } = new List<CachedUserAccess>();

        internal void Initialize()
        {
            var teamById = Teams.ToDictionary(t => t.Id);
            var repoByName = Repos.ToDictionary(r => r.Name);

            foreach (var repo in Repos)
            {
                repo.Org = this;
            }

            foreach (var team in Teams)
            {
                if (!string.IsNullOrEmpty(team.ParentId))
                {
                    team.Parent = teamById[team.ParentId];
                    team.Parent.Children.Add(team);
                }

                foreach (var repoAccess in team.Repos)
                {
                    repoAccess.Team = team;

                    if (repoByName.TryGetValue(repoAccess.RepoName, out var repo))
                    {
                        repoAccess.Repo = repo;
                        repo.Teams.Add(repoAccess);
                    }
                }

                team.Org = this;
                team.Repos.RemoveAll(r => r.Repo == null);
            }

            foreach (var collaborator in Collaborators)
            {
                if (repoByName.TryGetValue(collaborator.RepoName, out var repo))
                {
                    collaborator.Repo = repo;
                    repo.Users.Add(collaborator);
                }
            }

            Collaborators.RemoveAll(c => c.Repo == null);
        }

        public string DescribeAccess(CachedUserAccess collaborator)
        {
            return DescribeAccess(collaborator.RepoName, collaborator.User, collaborator.Permission);
        }

        public string DescribeAccess(string repoName, string user, CachedPermission level)
        {
            var repo = Repos.SingleOrDefault(r => r.Name == repoName);
            if (repo == null)
                return null;

            if (Owners.Contains(user))
                return "(Owner)";

            foreach (var repoAccess in repo.Teams)
            {
                if (repoAccess.Permission == level)
                {
                    foreach (var team in repoAccess.Team.DescendentsAndSelf())
                    {
                        if (team.Members.Contains(user))
                            return team.GetFullName();
                    }
                }
            }

            return "(Collaborator)";
        }

        public static string GetRepoUrl(string orgName, string repoName)
        {
            return $"https://github.com/{orgName}/{repoName}";
        }

        public static string GetTeamUrl(string orgName, string teamName)
        {
            return $"https://github.com/orgs/{orgName}/teams/{teamName.ToLower()}";
        }

        public static string GetUserUrl(string userName)
        {
            return $"https://github.com/{userName}";
        }
    }
}
