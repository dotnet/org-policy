using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Octokit;

using Terrajobst.Csv;

namespace GitHubPermissionSurveyor
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length < 1 || 2 < args.Length)
            {
                var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                Console.Error.WriteLine("error: wrong number of arguments");
                Console.Error.WriteLine($"usage {exeName} <org-name> [output-path]");
                return;
            }

            var orgName = args[0];
            var outputFileName = args.Length < 2 ? null : args[1];
            var isForExcel = outputFileName == null;

            if (outputFileName == null && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: you must specify and output path because you don't have Excel.");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var cachedOrg = await LoadCachedOrgAsync(client, orgName);

            var csvDocument = new CsvDocument("repo", "repo-state", "repo-last-pushed", "principal-kind", "principal", "permission", "via-team");
            using (var writer = csvDocument.Append())
            {
                foreach (var repo in cachedOrg.Repos)
                {
                    var publicPrivate = repo.IsPrivate ? "private" : "public";
                    var lastPush = repo.LastPush.ToLocalTime().DateTime.ToString();
                    var repoUrl = $"https://github.com/{cachedOrg.Name}/{repo.Name}";

                    foreach (var teamAccess in repo.Teams)
                    {
                        var permissions = teamAccess.Permission.ToString().ToLower();
                        var teamName = teamAccess.Team.Name;
                        var teamUrl = $"https://github.com/orgs/{cachedOrg.Name}/teams/{teamName.ToLower()}";

                        writer.Write(CreateHyperlink(isForExcel, repoUrl, repo.Name));
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("team");
                        writer.Write(CreateHyperlink(isForExcel, teamUrl, teamName));
                        writer.Write(permissions);
                        writer.Write(teamName);
                        writer.WriteLine();
                    }

                    foreach (var userAccess in repo.Users)
                    {
                        var via = cachedOrg.DescribeAccess(userAccess);
                        var userUrl = $"https://github.com/{userAccess.User}";
                        var permissions = userAccess.Permission.ToString().ToLower();

                        writer.Write(CreateHyperlink(isForExcel, repoUrl, repo.Name));
                        writer.Write(publicPrivate);
                        writer.Write(lastPush);
                        writer.Write("user");
                        writer.Write(CreateHyperlink(isForExcel, userUrl, userAccess.User));
                        writer.Write(permissions);
                        writer.Write(via);
                        writer.WriteLine();
                    }
                }
            }

            if (outputFileName == null)
            {
                csvDocument.ViewInExcel();
            }
            else
            {
                var extension = Path.GetExtension(outputFileName);
                if (extension == ".md")
                    csvDocument.SaveAsMarkdownTable(outputFileName);
                else
                    csvDocument.Save(outputFileName);
            }
        }

        private static string CreateHyperlink(bool useFormula, string url, string text)
        {
            return useFormula
                    ? $"=HYPERLINK(\"{url}\", \"{text}\")"
                    : text;
        }

        private static async Task<CachedOrg> LoadCachedOrgAsync(GitHubClient client, string orgName)
        {
            var cachedOrg = await CachedOrg.LoadAsync(orgName);
            if (cachedOrg == null)
            {
                cachedOrg = await LoadCachedOrgFromGitHubAsync(client, orgName);
                await cachedOrg.SaveAsync();
            }

            return cachedOrg;
        }

        private static async Task<CachedOrg> LoadCachedOrgFromGitHubAsync(GitHubClient client, string orgName)
        {
            Console.WriteLine("Loading org data from GitHub...");

            var cachedOrg = new CachedOrg();
            cachedOrg.Name = orgName;

            await LoadOwnersAsync(client, cachedOrg);
            await LoadTeamsAsync(client, cachedOrg);
            await LoadReposAndCollaboratorsAsync(client, cachedOrg);

            cachedOrg.Initialize();

            return cachedOrg;
        }

        private static async Task LoadOwnersAsync(GitHubClient client, CachedOrg cachedOrg)
        {
            var owners = await client.Organization.Member.GetAll(cachedOrg.Name, OrganizationMembersFilter.All, OrganizationMembersRole.Admin, ApiOptions.None);
            foreach (var owner in owners)
                cachedOrg.Owners.Add(owner.Login);
        }

        private static async Task LoadTeamsAsync(GitHubClient client, CachedOrg cachedOrg)
        {
            var teams = await client.Organization.Team.GetAll(cachedOrg.Name);

            var i = 0;

            foreach (var team in teams)
            {
                PrintRateLimit(client);
                PrintPercentage(i++, teams.Count, team.Name);

                var cachedTeam = new CachedTeam
                {
                    Id = team.Id.ToString(),
                    ParentId = team.Parent?.Id.ToString(),
                    Name = team.Name
                };
                cachedOrg.Teams.Add(cachedTeam);

                var request = new TeamMembersRequest(TeamRoleFilter.All);
                var members = await client.Organization.Team.GetAllMembers(team.Id, request);

                foreach (var member in members)
                    cachedTeam.Members.Add(member.Login);

                foreach (var repo in await client.Organization.Team.GetAllRepositories(team.Id))
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

        private static async Task LoadReposAndCollaboratorsAsync(GitHubClient client, CachedOrg cachedOrg)
        {
            var repos = await client.Repository.GetAllForOrg(cachedOrg.Name);
            var i = 0;

            foreach (var repo in repos)
            {
                PrintRateLimit(client);
                PrintPercentage(i++, repos.Count, repo.FullName);

                var cachedRepo = new CachedRepo
                {
                    Name = repo.Name,
                    IsPrivate = repo.Private,
                    LastPush = repo.PushedAt ?? repo.CreatedAt
                };
                cachedOrg.Repos.Add(cachedRepo);

                foreach (var user in await client.Repository.Collaborator.GetAll(repo.Owner.Login, repo.Name))
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

        private static void PrintPercentage(int currentItem, int itemCount, string text)
        {
            var percentage = currentItem / (float)itemCount;
            Console.WriteLine($"{text} {percentage:P1}...");
        }

        private static void PrintRateLimit(GitHubClient client)
        {
            var apiInfo = client.GetLastApiInfo();
            if (apiInfo?.RateLimit != null)
                Console.WriteLine($"Remaining: {apiInfo.RateLimit.Remaining}, Reset={apiInfo.RateLimit.Reset}");
        }
    }
}
