using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.Ospo;

using Octokit;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    internal sealed class CacheLoader
    {
        public CacheLoader(GitHubClient gitHubClient, TextWriter logWriter, OspoClient ospoClient)
        {
            GitHubClient = gitHubClient;
            Log = logWriter ?? Console.Out;
            OspoClient = ospoClient;
        }

        public GitHubClient GitHubClient { get; }
        public TextWriter Log { get; }
        public OspoClient OspoClient { get; }

        public async Task<CachedOrg> LoadAsync(string orgName)
        {
            var start = DateTimeOffset.Now;

            Log.WriteLine($"Start: {start}");
            Log.WriteLine($"Downloading '{orgName}' org from GitHub...");

            var cachedOrg = new CachedOrg
            {
                Version = CachedOrg.CurrentVersion,
                Name = orgName
            };

            await LoadMembersAsync(cachedOrg);
            await LoadTeamsAsync(cachedOrg);
            await LoadReposAndCollaboratorsAsync(cachedOrg);
            await LoadExternalUsersAsync(cachedOrg);
            await LoadUsersDetailsAsync(cachedOrg);

            var finish = DateTimeOffset.Now;
            var duration = finish - start;
            Log.WriteLine($"Finished downloading org {orgName}: {finish}. Took {duration}.");

            cachedOrg.Initialize();

            return cachedOrg;
        }

        private async Task LoadMembersAsync(CachedOrg cachedOrg)
        {
            await GitHubClient.PrintProgressAsync(Log, "Loading owner list");
            var owners = await GitHubClient.Organization.Member.GetAll(cachedOrg.Name, OrganizationMembersFilter.All, OrganizationMembersRole.Admin, ApiOptions.None);

            await GitHubClient.PrintProgressAsync(Log, "Loading non-owner list");
            var nonOwners = await GitHubClient.Organization.Member.GetAll(cachedOrg.Name, OrganizationMembersFilter.All, OrganizationMembersRole.Member, ApiOptions.None);

            foreach (var owner in owners)
            {
                var member = new CachedUser
                {
                    Login = owner.Login,
                    IsMember = true,
                    IsOwner = true
                };
                cachedOrg.Users.Add(member);
            }

            foreach (var nonOwner in nonOwners)
            {
                var member = new CachedUser
                {
                    Login = nonOwner.Login,
                    IsMember = true,
                    IsOwner = false
                };
                cachedOrg.Users.Add(member);
            }
        }

        private async Task LoadTeamsAsync(CachedOrg cachedOrg)
        {
            await GitHubClient.PrintProgressAsync(Log, "Loading team list");
            var teams = await GitHubClient.Organization.Team.GetAll(cachedOrg.Name);
            var i = 0;

            foreach (var team in teams)
            {
                await GitHubClient.PrintProgressAsync(Log, "Loading team", team.Name, i++, teams.Count);

                var cachedTeam = new CachedTeam
                {
                    Id = team.Id.ToString(),
                    ParentId = team.Parent?.Id.ToString(),
                    Name = team.Name,
                    Description = team.Description,
                    IsSecret = team.Privacy.Value == TeamPrivacy.Secret
                };
                cachedOrg.Teams.Add(cachedTeam);

                var maintainerRequest = new TeamMembersRequest(TeamRoleFilter.Maintainer);
                var maintainers = await GitHubClient.Organization.Team.GetAllMembers(team.Id, maintainerRequest);

                foreach (var maintainer in maintainers)
                    cachedTeam.MaintainerLogins.Add(maintainer.Login);

                await GitHubClient.WaitForEnoughQuotaAsync(Log);

                var memberRequest = new TeamMembersRequest(TeamRoleFilter.All);
                var members = await GitHubClient.Organization.Team.GetAllMembers(team.Id, memberRequest);

                foreach (var member in members)
                    cachedTeam.MemberLogins.Add(member.Login);

                await GitHubClient.WaitForEnoughQuotaAsync(Log);

                foreach (var repo in await GitHubClient.Organization.Team.GetAllRepositories(team.Id))
                {
                    var permission = repo.Permissions.Admin
                                        ? CachedPermission.Admin
                                        : repo.Permissions.Push
                                            ? CachedPermission.Push
                                            : CachedPermission.Pull;

                    var cachedRepoAccess = new CachedTeamAccess
                    {
                        RepoName = repo.Name,
                        Permission = permission
                    };
                    cachedTeam.Repos.Add(cachedRepoAccess);
                }
            }
        }

        private async Task LoadReposAndCollaboratorsAsync(CachedOrg cachedOrg)
        {
            await GitHubClient.PrintProgressAsync(Log, "Loading repo list");
            var repos = await GitHubClient.Repository.GetAllForOrg(cachedOrg.Name);
            var i = 0;

            foreach (var repo in repos)
            {
                await GitHubClient.PrintProgressAsync(Log, "Loading repo", repo.FullName, i++, repos.Count);

                var cachedRepo = new CachedRepo
                {
                    Name = repo.Name,
                    IsPrivate = repo.Private,
                    IsArchived = repo.Archived,
                    LastPush = repo.PushedAt ?? repo.CreatedAt,
                    Description = repo.Description
                };
                cachedOrg.Repos.Add(cachedRepo);

                try
                {
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
                            UserLogin = user.Login,
                            Permission = permission
                        };
                        cachedOrg.Collaborators.Add(cachedCollaborator);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"error: {ex.Message}");
                }
            }
        }

        private async Task LoadExternalUsersAsync(CachedOrg cachedOrg)
        {
            await GitHubClient.PrintProgressAsync(Log, "Loading outside collaborators");
            var outsideCollaborators = await GitHubClient.Organization.OutsideCollaborator.GetAll(cachedOrg.Name, OrganizationMembersFilter.All, ApiOptions.None);

            foreach (var user in outsideCollaborators)
            {
                var cachedUser = new CachedUser
                {
                    Login = user.Login,
                    IsOwner = false,
                    IsMember = false
                };
                cachedOrg.Users.Add(cachedUser);
            }
        }

        private async Task LoadUsersDetailsAsync(CachedOrg cachedOrg)
        {
            var linkSet = await LoadLinkSetAsync();

            var i = 0;

            foreach (var cachedUser in cachedOrg.Users)
            {
                await GitHubClient.PrintProgressAsync(Log, "Loading user details", cachedUser.Login, i++, cachedOrg.Users.Count);

                var user = await GitHubClient.User.Get(cachedUser.Login);
                cachedUser.Name = user.Name;
                cachedUser.Company = user.Company;
                cachedUser.Email = user.Email;

                if (linkSet.LinkByLogin.TryGetValue(cachedUser.Login, out var link))
                    cachedUser.MicrosoftInfo = link?.MicrosoftInfo;
            }
        }

        private async Task<OspoLinkSet> LoadLinkSetAsync()
        {
            if (OspoClient == null)
                return new OspoLinkSet();

            Log.WriteLine("Loading Microsoft link information...");
            return await OspoClient.GetAllAsync();
        }
    }
}
