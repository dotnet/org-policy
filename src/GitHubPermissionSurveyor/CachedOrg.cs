
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GitHubPermissionSurveyor
{
    internal class CachedOrg
    {
        public string Name { get; set; }
        public List<string> Owners { get; set; } = new List<string>();
        public List<CachedTeam> Teams { get; set; } = new List<CachedTeam>();
        public List<CachedRepo> Repos { get; set; } = new List<CachedRepo>();
        public List<CachedUserAccess> Collaborators { get; set; } = new List<CachedUserAccess>();

        public void Initialize()
        {
            var teamById = Teams.ToDictionary(t => t.Id);
            var repoByName = Repos.ToDictionary(r => r.Name);

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

        private static string GetCachedPath()
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var exeDir = Path.GetDirectoryName(exePath);
            return Path.Combine(exeDir, "cached-org.json");
        }

        public static async Task<CachedOrg> LoadAsync(string orgName)
        {
            var path = GetCachedPath();
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                var orgData = await JsonSerializer.DeserializeAsync<CachedOrg>(stream, options);
                orgData.Initialize();

                if (orgData.Name != orgName)
                    return null;

                return orgData;
            }
        }

        public async Task SaveAsync()
        {
            var path = GetCachedPath();
            using (var stream = File.Create(path))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                await JsonSerializer.SerializeAsync(stream, this, options);
            }
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
    }
}
