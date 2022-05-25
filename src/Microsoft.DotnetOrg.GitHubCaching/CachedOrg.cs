using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotnetOrg.Ospo;

using Octokit.GraphQL;

namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class CachedOrg
{
    public static int CurrentVersion => 14;

    public int Version { get; set; }
    public string Name { get; set; }
    public CachedActionPermissions ActionPermissions { get; set; }
    public List<CachedOrgSecret> Secrets { get; set; } = new List<CachedOrgSecret>();
    public List<CachedTeam> Teams { get; set; } = new List<CachedTeam>();
    public List<CachedRepo> Repos { get; set; } = new List<CachedRepo>();
    public List<CachedUserAccess> Collaborators { get; set; } = new List<CachedUserAccess>();
    public List<CachedTeamAccess> TeamAccess { get; set; } = new List<CachedTeamAccess>();
    public List<CachedUser> Users { get; set; } = new List<CachedUser>();

    public void Initialize()
    {
        if (Version != CurrentVersion)
            return;

        var teamById = Teams.ToDictionary(t => t.Id);
        var repoByName = Repos.ToDictionary(r => r.Name);
        var userByLogin = Users.ToDictionary(u => u.Login);

        foreach (var secret in Secrets)
        {
            secret.Org = this;
            secret.Repositories = secret.RepositoryNames.Select(r => repoByName.TryGetValue(r, out var repo) ? repo : null)
                .Where(r => r is not null)
                .Select(r => r!)
                .ToArray();

            foreach (var repo in secret.Repositories)
                repo.OrgSecrets.Add(secret);
        }

        foreach (var repo in Repos)
        {
            repo.Org = this;

            foreach (var branch in repo.Branches)
                branch.Repo = repo;

            foreach (var rule in repo.BranchProtectionRules)
                rule.Repo = repo;

            foreach (var environment in repo.Environments)
            {
                environment.Repo = repo;

                foreach (var secret in environment.Secrets)
                {
                    secret.Repo = repo;
                    secret.Environment = environment;
                }
            }

            foreach (var secret in repo.Secrets)
            {
                secret.Repo = repo;
            }
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

            team.Repos.RemoveAll(r => r.Repo is null);
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

        Collaborators.RemoveAll(c => c.Repo is null || c.User is null);

        foreach (var user in Users)
        {
            user.Org = this;
        }

        foreach (var team in Teams)
        {
            var effectiveMembers = team.DescendentsAndSelf().SelectMany(t => t.Members).Distinct();
            team.EffectiveMembers.AddRange(effectiveMembers);
        }

        var orgOwners = Users.Where(u => u.IsOwner).ToArray();

        foreach (var repo in Repos)
        {
            var effectiveUsers = new Dictionary<CachedUser, CachedUserAccess>();

            foreach (var orgOwner in orgOwners)
            {
                effectiveUsers.Add(orgOwner, new CachedUserAccess
                {
                    Repo = repo,
                    RepoName = Name,
                    User = orgOwner,
                    UserLogin = orgOwner.Login,
                    Permission = CachedPermission.Admin
                });
            }

            foreach (var userAccess in repo.Users)
            {
                if (!userAccess.User.IsOwner)
                    effectiveUsers.Add(userAccess.User, userAccess);
            }

            foreach (var teamAccess in repo.Teams)
            {
                foreach (var user in teamAccess.Team.EffectiveMembers)
                {
                    if (effectiveUsers.TryGetValue(user, out var userAccess))
                    {
                        if (userAccess.Permission >= teamAccess.Permission)
                            continue;
                    }

                    effectiveUsers[user] = new CachedUserAccess
                    {
                        Repo = repo,
                        RepoName = Name,
                        User = user,
                        UserLogin = user.Login,
                        Permission = teamAccess.Permission
                    };
                }
            }

            repo.EffectiveUsers.AddRange(effectiveUsers.Values);
        }
    }

    public static string GetRepoUrl(string orgName, string repoName)
    {
        return $"https://github.com/{orgName}/{repoName}";
    }

    public static string GetBranchUrl(string orgName, string repoName, string branchName)
    {
        return $"https://github.com/{orgName}/{repoName}/tree/{branchName}";
    }

    public static string GetTeamUrl(string orgName, string teamSlug)
    {
        return $"https://github.com/orgs/{orgName}/teams/{teamSlug}";
    }

    public static string GetUserUrl(string login)
    {
        return $"https://github.com/{login}";
    }

    public static Task<CachedOrg?> LoadAsync(Octokit.GitHubClient client,
        Connection connection,
        string orgName,
        TextWriter? logWriter = null,
        OspoClient? ospoClient = null)
    {
        var loader = new CacheLoader(client, connection, logWriter, ospoClient);
        return loader.LoadAsync(orgName)!;
    }

    public static async Task<CachedOrg?> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        using (var stream = File.OpenRead(path))
            return await LoadAsync(stream);
    }

    public static async Task<CachedOrg?> LoadAsync(Stream stream)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        var orgData = await JsonSerializer.DeserializeAsync<CachedOrg>(stream, options);
        if (orgData is null)
            return null;
        orgData.Initialize();
        return orgData;
    }

    public async Task SaveAsync(string path)
    {
        var cacheDirectory = Path.GetDirectoryName(path)!;
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
#pragma warning restore CS8618