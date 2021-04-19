using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.Ospo;

using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;

using static Octokit.GraphQL.Variable;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    internal sealed class CacheLoader
    {
        public CacheLoader(Connection connection, TextWriter? logWriter, OspoClient? ospoClient)
        {
            Connection = connection;
            Log = logWriter ?? Console.Out;
            OspoClient = ospoClient;
        }

        public int ErrorRetryCount { get; set; } = 3;
        public Connection Connection { get; }
        public TextWriter Log { get; }
        public OspoClient? OspoClient { get; }

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

            Log.WriteLine($"Loading members...");
            var members = await GetCachedMembersAsync(orgName);
            cachedOrg.Users.AddRange(members);

            Log.WriteLine($"Loading repos...");
            var repos = await GetCachedReposAsync(orgName);
            cachedOrg.Repos.AddRange(repos);

            Log.WriteLine($"Loading teams...");
            var teams = await GetCachedTeamsAsync(orgName);
            cachedOrg.Teams.AddRange(teams);

            var repoNames = repos.Where(r => !r.IsArchived).Select(r => r.Name).ToArray();
            var teamBySlug = teams.ToDictionary(t => t.Slug);
            var teamSlugs = teamBySlug.Keys;

            Log.WriteLine($"Loading team members...");
            var teamMembers = await GetCachedTeamMembersAsync(orgName, teamSlugs);

            foreach (var teamMember in teamMembers)
            {
                var team = teamBySlug[teamMember.TeamSlug];

                team.MemberLogins.Add(teamMember.UserLogin);
                if (teamMember.IsMaintainer)
                    team.MaintainerLogins.Add(teamMember.UserLogin);
            }

            Log.WriteLine($"Loading externals...");
            var externals = await GetCachedExternalsAsync(orgName, repoNames);
            cachedOrg.Users.AddRange(externals);

            Log.WriteLine($"Loading user access...");
            var userAccess = await GetCachedUserAccessAsync(orgName, repoNames);
            cachedOrg.Collaborators.AddRange(userAccess);

            Log.WriteLine($"Loading team access...");
            var teamAccesses = await GetCachedTeamAccessAsync(orgName, teamSlugs);

            foreach (var teamAccess in teamAccesses)
            {
                var team = teamBySlug[teamAccess.TeamSlug];
                team.Repos.Add(teamAccess);
            }

            var linkSet = await LoadLinkSetAsync();

            foreach (var user in cachedOrg.Users)
            {
                if (linkSet.LinkByLogin.TryGetValue(user.Login, out var link))
                    user.MicrosoftInfo = link.MicrosoftInfo;
            }

            var finish = DateTimeOffset.Now;
            var duration = finish - start;
            Log.WriteLine($"Finished downloading org {orgName}: {finish}. Took {duration}.");

            cachedOrg.Initialize();

            return cachedOrg;
        }

        private async Task<OspoLinkSet> LoadLinkSetAsync()
        {
            if (OspoClient is null)
                return new OspoLinkSet();

            Log.WriteLine("Loading Microsoft link information...");
            var result = await OspoClient.GetAllAsync();

            if (result.Links.Count == 0)
            {
                throw new OspoException(
                    "The OSPO result set is empty. " +
                    "This very likely indicates a temporary issue with linking service. " +
                    "We have failed the build to avoid cascading errors due to missing" +
                    "linking data.");
            }

            return result;
        }

        private async Task<IReadOnlyCollection<CachedRepo>> GetCachedReposAsync(string orgName)
        {
            var query = new Query()
                .Organization(orgName)
                .Repositories()
                .AllPages()
                .Select(r => new CachedRepo()
                {
                    Name = r.Name,
                    LastPush = r.PushedAt ?? r.CreatedAt,
                    IsPrivate = r.IsPrivate,
                    IsFork = r.IsFork,
                    IsMirror = r.IsMirror,
                    IsArchived = r.IsArchived,
                    IsTemplate = r.IsTemplate,
                    Description = r.Description,
                    // This would be nice, but it doesn't work.
                    // https://github.com/octokit/octokit.graphql.net/issues/242
                    // DefaultBranch = r.DefaultBranchRef.Name,
                    // Branches = r.Refs("refs/heads/", null, null, null, null, null, null).AllPages().Select(r => r.Name).ToList()
                });

            var result = (await RunQueryWithRetry(query)).ToArray();

            var repoQueryArguments = new Dictionary<string, object?>();
            var repoQuery = new Query()
                .Repository(Var("repo"), orgName)
                .Select(r => new
                {
                    DefaultBranch = r.DefaultBranchRef == null ? "" : r.DefaultBranchRef.Name,
                    Branches = r.Refs("refs/heads/", null, null, null, null, null, null).AllPages().Select(r => 
                    new CachedBranch
                    {
                        Prefix = r.Prefix,
                        Name = r.Name,
                        Hash = r.Id.Value
                    }).ToList()
                })
                .Compile();

            foreach (var repo in result)
            {
                repoQueryArguments["repo"] = repo.Name;

                var branchInfo = await RunQueryWithRetry(repoQuery, repoQueryArguments);
                repo.DefaultBranch = branchInfo.DefaultBranch;
                repo.Branches = branchInfo.Branches;
            }

            return result;
        }

        private async Task<IReadOnlyCollection<CachedTeam>> GetCachedTeamsAsync(string orgName)
        {
            var query = new Query()
                .Organization(orgName)
                .Teams()
                .AllPages()
                .Select(t => new CachedTeam()
                {
                    Name = t.Name,
                    Id = t.Id.Value,
                    Slug = t.Slug,
                    ParentId = t.ParentTeam == null ? null : t.ParentTeam.Id.Value,
                    Description = t.Description,
                    IsSecret = t.Privacy == TeamPrivacy.Visible ? false : true
                });

            var result = await RunQueryWithRetry(query);
            return result.ToArray();
        }

        private async Task<IReadOnlyCollection<CachedTeamMember>> GetCachedTeamMembersAsync(string orgName, IEnumerable<string> teamSlugs)
        {
            var query = new Query()
                .Organization(orgName)
                .Team(Var("teamSlug"))
                .Members(first: 100, after: Var("after"), membership: TeamMembershipType.Immediate)
                .Select(connection => new
                {
                    connection.PageInfo.EndCursor,
                    connection.PageInfo.HasNextPage,
                    connection.TotalCount,
                    Items = connection.Edges.Select(e => new
                    {
                        e.Node.Login,
                        e.Role
                    }).ToList(),
                }).Compile();

            var result = new List<CachedTeamMember>();

            foreach (var teamSlug in teamSlugs)
            {
                var vars = new Dictionary<string, object?>
                {
                    { "after", null },
                    { "teamSlug", teamSlug },
                };

                var current = await RunQueryWithRetry(query, vars);
                vars["after"] = current.HasNextPage ? current.EndCursor : null;

                while (vars["after"] is not null)
                {
                    var page = await RunQueryWithRetry(query, vars);
                    current.Items.AddRange(page.Items);
                    vars["after"] = page.HasNextPage
                                        ? page.EndCursor
                                        : null;
                }

                foreach (var item in current.Items)
                {
                    var isMaintainer = item.Role switch
                    {
                        TeamMemberRole.Member => false,
                        TeamMemberRole.Maintainer => true,
                        _ => throw new NotImplementedException($"Unexpected role {item.Role}"),
                    };
                    var cachedTeamMember = new CachedTeamMember
                    {
                        TeamSlug = teamSlug,
                        UserLogin = item.Login,
                        IsMaintainer = isMaintainer
                    };
                    result.Add(cachedTeamMember);
                }
            }

            return result.ToArray();
        }

        private async Task<IReadOnlyCollection<CachedTeamAccess>> GetCachedTeamAccessAsync(string orgName, IEnumerable<string> teamSlugs)
        {
            var query = new Query()
                .Organization(orgName)
                .Team(Var("teamSlug"))
                .Repositories(first: 100, after: Var("after"))
                .Select(connection => new
                {
                    connection.PageInfo.EndCursor,
                    connection.PageInfo.HasNextPage,
                    connection.TotalCount,
                    Items = connection.Edges.Select(e => new
                    {
                        e.Node.Name,
                        e.Permission
                    }).ToList(),
                }).Compile();

            var result = new List<CachedTeamAccess>();

            foreach (var teamSlug in teamSlugs)
            {
                var vars = new Dictionary<string, object?>
                {
                    { "after", null },
                    { "teamSlug", teamSlug },
                };

                var current = await RunQueryWithRetry(query, vars);
                vars["after"] = current.HasNextPage ? current.EndCursor : null;

                while (vars["after"] is not null)
                {
                    var page = await RunQueryWithRetry(query, vars);
                    current.Items.AddRange(page.Items);
                    vars["after"] = page.HasNextPage
                                        ? page.EndCursor
                                        : null;
                }

                foreach (var item in current.Items)
                {
                    var cachedTeamMember = new CachedTeamAccess
                    {
                        TeamSlug = teamSlug,
                        RepoName = item.Name,
                        Permission = GetCachedPermission(item.Permission)
                    };
                    result.Add(cachedTeamMember);
                }
            }

            return result.ToArray();
        }

        private async Task<IReadOnlyCollection<CachedUser>> GetCachedMembersAsync(string orgName)
        {
            var query = new Query()
                .Organization(orgName)
                .MembersWithRole(first: 100, after: Var("after"))
                .Select(connection => new
                {
                    connection.PageInfo.EndCursor,
                    connection.PageInfo.HasNextPage,
                    connection.TotalCount,
                    Items = connection.Edges.Select(e => new
                    {
                        e.Node.Login,
                        e.Node.Name,
                        e.Node.Company,
                        e.Node.Email,
                        e.Role,
                    }).ToList(),
                }).Compile();

            var result = new List<CachedUser>();

            var vars = new Dictionary<string, object?>
            {
                { "after", null },
            };

            var current = await RunQueryWithRetry(query, vars);
            vars["after"] = current.HasNextPage ? current.EndCursor : null;

            while (vars["after"] is not null)
            {
                var page = await RunQueryWithRetry(query, vars);
                current.Items.AddRange(page.Items);
                vars["after"] = page.HasNextPage
                                    ? page.EndCursor
                                    : null;
            }

            foreach (var item in current.Items)
            {
                var isMember = item.Role is not null;
                var isAdmin = item.Role switch
                {
                    OrganizationMemberRole.Member => false,
                    OrganizationMemberRole.Admin => true,
                    _ => throw new NotImplementedException($"Unexpected role {item.Role}"),
                };
                var cachedUser = new CachedUser
                {
                    Login = item.Login,
                    Name = item.Name,
                    Company = item.Company,
                    Email = item.Email,
                    IsOwner = isAdmin,
                    IsMember = isMember,
                };
                result.Add(cachedUser);
            }

            return result.ToArray();
        }

        private async Task<IReadOnlyCollection<CachedUser>> GetCachedExternalsAsync(string orgName, IEnumerable<string> repoNames)
        {
            var query = new Query()
                .Repository(Var("repoName"), orgName)
                .Collaborators(affiliation: CollaboratorAffiliation.Outside)
                .AllPages()
                .Select(e => new
                {
                    e.Login,
                    e.Name,
                    e.Company,
                    e.Email,
                })
                .Compile();

            var seenLogins = new HashSet<string>();
            var result = new List<CachedUser>();

            foreach (var repoName in repoNames)
            {
                var vars = new Dictionary<string, object?>
                {
                    { "repoName", repoName },
                };

                var queryResult = await RunQueryWithRetry(query, vars);

                foreach (var item in queryResult)
                {
                    if (seenLogins.Add(item.Login))
                    {
                        var cachedUser = new CachedUser
                        {
                            Login = item.Login,
                            Name = item.Name,
                            Company = item.Company,
                            Email = item.Email,
                            IsOwner = false,
                            IsMember = false
                        };
                        result.Add(cachedUser);
                    }
                }
            }

            return result.ToArray();
        }

        private async Task<IReadOnlyCollection<CachedUserAccess>> GetCachedUserAccessAsync(string orgName, IEnumerable<string> repoNames)
        {
            var query = new Query()
                .Repository(Var("repoName"), orgName)
                .Collaborators(first: 100, after: Var("after"), affiliation: CollaboratorAffiliation.Direct)
                .Select(connection => new
                {
                    connection.PageInfo.EndCursor,
                    connection.PageInfo.HasNextPage,
                    connection.TotalCount,
                    Items = connection.Edges.Select(e => new
                    {
                        e.Node.Login,
                        e.Permission,
                    }).ToList(),
                }).Compile();

            var result = new List<CachedUserAccess>();

            foreach (var repoName in repoNames)
            {
                var vars = new Dictionary<string, object?>
                {
                    { "after", null },
                    { "repoName", repoName },
                };

                var current = await RunQueryWithRetry(query, vars);
                vars["after"] = current.HasNextPage ? current.EndCursor : null;

                while (vars["after"] is not null)
                {
                    var page = await RunQueryWithRetry(query, vars);
                    current.Items.AddRange(page.Items);
                    vars["after"] = page.HasNextPage
                                        ? page.EndCursor
                                        : null;
                }

                foreach (var item in current.Items)
                {
                    var cachedPermission = GetCachedPermission(item.Permission);
                    var cachedCollaborator = new CachedUserAccess
                    {
                        RepoName = repoName,
                        UserLogin = item.Login,
                        Permission = cachedPermission
                    };
                    result.Add(cachedCollaborator);
                }
            }

            return result.ToArray();
        }

        private Task<IEnumerable<T>> RunQueryWithRetry<T>(IQueryableList<T> query)
        {
            return RunQueryWithRetry(() => Connection.Run(query));
        }

        private Task<T> RunQueryWithRetry<T>(ICompiledQuery<T> query, Dictionary<string, object?> variables)
        {
            return RunQueryWithRetry(() => Connection.Run(query, variables));
        }

        private async Task<T> RunQueryWithRetry<T>(Func<Task<T>> func)
        {
            var attempt = 1;

        TryAgain:
            try
            {
                return await func();
            }
            catch (NullReferenceException)
            {
                Log.WriteLine($"error: API quota exceeded");
                await Task.Delay(TimeSpan.FromMinutes(65));
                goto TryAgain;
            }
            catch (Octokit.AbuseException ex)
            {
                await ex.HandleAsync();
                goto TryAgain;
            }
            catch (Exception ex) when (attempt < ErrorRetryCount)
            {
                Log.WriteLine($"error on attempt {attempt} of {ErrorRetryCount}: {ex.Message}");
                attempt++;
                await Task.Delay(TimeSpan.FromMinutes(5));
                goto TryAgain;
            }
        }

        private static CachedPermission GetCachedPermission(RepositoryPermission permission)
        {
            return permission switch
            {
                RepositoryPermission.Admin => CachedPermission.Admin,
                RepositoryPermission.Maintain => CachedPermission.Maintain,
                RepositoryPermission.Write => CachedPermission.Write,
                RepositoryPermission.Triage => CachedPermission.Triage,
                RepositoryPermission.Read => CachedPermission.Read,
                _ => throw new NotImplementedException($"Unexpected permision {permission}"),
            };
        }
    }
}
