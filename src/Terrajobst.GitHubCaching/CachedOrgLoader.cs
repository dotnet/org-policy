using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Octokit;

namespace Terrajobst.GitHubCaching
{
    public sealed class CachedOrgLoader
    {
        public CachedOrgLoader(GitHubClient gitHubClient, TextWriter logWriter, bool forceUpdate)
        {
            GitHubClient = gitHubClient;
            LogWriter = logWriter;
            ForceUpdate = forceUpdate;
        }

        public GitHubClient GitHubClient { get; }
        public TextWriter LogWriter { get; }
        public bool ForceUpdate { get; }

        private string GetCachedPath(string orgName)
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachedDirectory = Path.Combine(localData, "GitHubPermissionSurveyor", "Cache");
            return Path.Combine(cachedDirectory, $"{orgName}.json");
        }

        public async Task<CachedOrg> LoadAsync(string orgName)
        {
            var cachedOrg = ForceUpdate
                                ? null
                                : await LoadFromCacheAsync(orgName);

            if (cachedOrg == null)
            {
                cachedOrg = await LoadFromGitHubAsync(orgName);
                await SaveToCacheAsync(orgName);
            }

            return cachedOrg;
        }

        private async Task<CachedOrg> LoadFromCacheAsync(string orgName)
        {
            var path = GetCachedPath(orgName);
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

        private async Task SaveToCacheAsync(string orgName)
        {
            var path = GetCachedPath(orgName);
            var cacheDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(cacheDirectory);

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

        private async Task<CachedOrg> LoadFromGitHubAsync(string orgName)
        {
            LogWriter.WriteLine("Loading org data from GitHub...");

            var cachedOrg = new CachedOrg
            {
                Name = orgName
            };

            await LoadOwnersAsync(cachedOrg);
            await LoadTeamsAsync(cachedOrg);
            await LoadReposAndCollaboratorsAsync(cachedOrg);

            cachedOrg.Initialize();

            return cachedOrg;
        }

        private async Task LoadOwnersAsync(CachedOrg cachedOrg)
        {
            var owners = await GitHubClient.Organization.Member.GetAll(cachedOrg.Name, OrganizationMembersFilter.All, OrganizationMembersRole.Admin, ApiOptions.None);
            foreach (var owner in owners)
                cachedOrg.Owners.Add(owner.Login);
        }

        private async Task LoadTeamsAsync(CachedOrg cachedOrg)
        {
            var teams = await GitHubClient.Organization.Team.GetAll(cachedOrg.Name);

            var i = 0;

            foreach (var team in teams)
            {
                PrintRateLimit(GitHubClient);
                PrintPercentage(i++, teams.Count, team.Name);

                var cachedTeam = new CachedTeam
                {
                    Id = team.Id.ToString(),
                    ParentId = team.Parent?.Id.ToString(),
                    Name = team.Name
                };
                cachedOrg.Teams.Add(cachedTeam);

                var request = new TeamMembersRequest(TeamRoleFilter.All);
                var members = await GitHubClient.Organization.Team.GetAllMembers(team.Id, request);

                foreach (var member in members)
                    cachedTeam.Members.Add(member.Login);

                foreach (var repo in await GitHubClient.Organization.Team.GetAllRepositories(team.Id))
                {
                    var permissionLevel = repo.Permissions.Admin
                                            ? CachedPermission.Admin
                                            : repo.Permissions.Push
                                                ? CachedPermission.Push
                                                : CachedPermission.Pull;

                    var cachedRepoAccess = new CachedTeamAccess
                    {
                        RepoName = repo.Name,
                        Permission = permissionLevel
                    };
                    cachedTeam.Repos.Add(cachedRepoAccess);
                }
            }
        }

        private async Task LoadReposAndCollaboratorsAsync(CachedOrg cachedOrg)
        {
            var repos = await GitHubClient.Repository.GetAllForOrg(cachedOrg.Name);
            var i = 0;

            foreach (var repo in repos)
            {
                PrintRateLimit(GitHubClient);
                PrintPercentage(i++, repos.Count, repo.FullName);

                var cachedRepo = new CachedRepo
                {
                    Name = repo.Name,
                    IsPrivate = repo.Private,
                    LastPush = repo.PushedAt ?? repo.CreatedAt
                };
                cachedOrg.Repos.Add(cachedRepo);

                foreach (var user in await GitHubClient.Repository.Collaborator.GetAll(repo.Owner.Login, repo.Name))
                {
                    var permission = user.Permissions.Admin
                                        ? CachedPermission.Admin
                                        : user.Permissions.Push
                                            ? CachedPermission.Push
                                            : CachedPermission.Pull;

                    var cachedCollaborator = new CachedUserAccess
                    {
                        RepoName = cachedRepo.Name,
                        User = user.Login,
                        Permission = permission
                    };
                    cachedOrg.Collaborators.Add(cachedCollaborator);
                }
            }
        }

        private void PrintPercentage(int currentItem, int itemCount, string text)
        {
            var percentage = currentItem / (float)itemCount;
            LogWriter.WriteLine($"{text} {percentage:P1}...");
        }

        private void PrintRateLimit(GitHubClient client)
        {
            var apiInfo = client.GetLastApiInfo();
            if (apiInfo?.RateLimit != null)
                LogWriter.WriteLine($"API rate limit remaining: {apiInfo.RateLimit.Remaining}, Reset={apiInfo.RateLimit.Reset}");
        }
    }
}
