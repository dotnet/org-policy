using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Octokit;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedOrg
    {
        public static int CurrentVersion = 3;

        public int Version { get; set; }
        public string Name { get; set; }
        public List<CachedTeam> Teams { get; set; } = new List<CachedTeam>();
        public List<CachedRepo> Repos { get; set; } = new List<CachedRepo>();
        public List<CachedUserAccess> Collaborators { get; set; } = new List<CachedUserAccess>();
        public List<CachedUser> Users { get; set; } = new List<CachedUser>();

        internal void Initialize()
        {
            if (Version != CurrentVersion)
                return;

            var teamById = Teams.ToDictionary(t => t.Id);
            var repoByName = Repos.ToDictionary(r => r.Name);
            var userByLogin = Users.ToDictionary(u => u.Login);

            foreach (var repo in Repos)
            {
                repo.Org = this;
            }

            foreach (var team in Teams)
            {
                team.Org = this;

                if (!string.IsNullOrEmpty(team.ParentId) && teamById.TryGetValue(team.ParentId, out var parentTeam))
                {
                    team.Parent = parentTeam;
                    parentTeam.Children.Add(team);
                }

                foreach (var maintainerLogin in team.MaintainerLogins)
                {
                    if (userByLogin.TryGetValue(maintainerLogin, out var maintainer))
                        team.Maintainers.Add(maintainer);
                }

                foreach (var memberLogin in team.MemberLogins)
                {
                    if (userByLogin.TryGetValue(memberLogin, out var member))
                    {
                        team.Members.Add(member);
                        member.Teams.Add(team);
                    }
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

                if (userByLogin.TryGetValue(collaborator.UserLogin, out var user))
                {
                    collaborator.User = user;
                    user.Repos.Add(collaborator);
                }
            }

            Collaborators.RemoveAll(c => c.Repo == null || c.User == null);

            foreach (var user in Users)
            {
                user.Org = this;
            }
        }

        public static string GetRepoUrl(string orgName, string repoName)
        {
            return $"https://github.com/{orgName}/{repoName}";
        }

        public static string GetTeamUrl(string orgName, string teamName)
        {
            var sb = new StringBuilder();
            foreach (var c in teamName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLower(c));
                }
                else
                {
                    sb.Append('-');
                }
            }

            var teamNameFixed = sb.ToString();
            return $"https://github.com/orgs/{orgName}/teams/{teamNameFixed}";
        }

        public static string GetUserUrl(string login)
        {
            return $"https://github.com/{login}";
        }

        public static async Task<CachedOrg> LoadAsync(GitHubClient gitHubClient,
                                                      string orgName,
                                                      TextWriter logWriter = null,
                                                      string cacheLocation = null,
                                                      bool forceUpdate = false)
        {
            var cachedOrg = forceUpdate
                    ? null
                    : await LoadFromCacheAsync(orgName, cacheLocation);

            if (cachedOrg == null)
            {
                var loader = new CacheLoader(gitHubClient, logWriter);
                cachedOrg = await loader.LoadAsync(orgName);
                await cachedOrg.SaveAsync(cacheLocation);
            }

            return cachedOrg;
        }

        public static string GetCacheLocation(string orgName)
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachedDirectory = Path.Combine(localAppData, fileInfo.CompanyName, fileInfo.ProductName, "Cache");
            return Path.Combine(cachedDirectory, $"{orgName}.json");
        }

        public static async Task<CachedOrg> LoadFromCacheAsync(string orgName, string cacheLocation = null)
        {
            var path = string.IsNullOrEmpty(cacheLocation)
                        ? GetCacheLocation(orgName)
                        : cacheLocation;

            var cachedOrg = await LoadAsync(path);

            var cacheIsValid = cachedOrg != null &&
                               cachedOrg.Name == orgName &&
                               cachedOrg.Version == CurrentVersion;

            return cacheIsValid ? cachedOrg : null;
        }

        public static async Task<CachedOrg> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
                return await LoadAsync(stream);
        }

        public static async Task<CachedOrg> LoadAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var orgData = await JsonSerializer.DeserializeAsync<CachedOrg>(stream, options);
            orgData.Initialize();
            return orgData;
        }

        public async Task SaveAsync(string path)
        {
            var cacheDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(cacheDirectory);

            using (var stream = File.Create(path))
                await SaveAsync(stream);
        }

        public async Task SaveAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            await JsonSerializer.SerializeAsync(stream, this, options);
        }
    }
}
