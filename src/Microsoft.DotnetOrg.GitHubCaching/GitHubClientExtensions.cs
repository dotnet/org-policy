using Octokit;
using System.Text;
using System.Text.Json;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public static class GitHubClientExtensions
    {
        public static async Task InvokeAsync<T>(this GitHubClient client, Func<GitHubClient, Task> operation)
        {
            await client.InvokeAsync<object?>(async c => {
                await operation(c);
                return null;
            });
        }

        public static async Task<T> InvokeAsync<T>(this GitHubClient client, Func<GitHubClient, Task<T>> operation)
        {
            var remainingRetries = 3;

            while (true)
            {
                try
                {
                    return await operation(client);
                }
                catch (RateLimitExceededException ex) when (remainingRetries > 0)
                {
                    var delay = ex.GetRetryAfterTimeSpan()
                                  .Add(TimeSpan.FromSeconds(15)); // Add some buffer
                    var until = DateTime.Now.Add(delay);

                    Console.WriteLine($"Rate limit exceeded. Waiting for {delay.TotalMinutes:N1} mins until {until}.");
                    await Task.Delay(delay);
                }
                catch (AbuseException ex) when (remainingRetries > 0)
                {
                    var delay = TimeSpan.FromSeconds(ex.RetryAfterSeconds ?? 120);
                    var until = DateTime.Now.Add(delay);

                    Console.WriteLine($"Abuse detection triggered. Waiting for {delay.TotalMinutes:N1} mins until {until}.");
                    await Task.Delay(delay);
                }
            }
        }

        public static async Task<GitHubCommunityProfile?> GetCommunityProfile(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var uri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/profile");
                var response = await client.InvokeAsync(c => c.Connection.Get<GitHubCommunityProfile>(uri, null, "application/vnd.github.scarlet-witch-preview+json"));
                return response.Body;
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public static async Task<string?> GetCommunityFile(this GitHubClient client, GitHubCommunityProfile.CommunityFile? file)
        {
            if (string.IsNullOrEmpty(file?.RawUrl))
                return null;

            try
            {
                var uri = new Uri(file.RawUrl);
                var response = await client.InvokeAsync(c => c.Connection.Get<string>(uri, null, null));
                return response.Body;
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public static async Task<IReadOnlyList<CachedOrgSecret>> GetOrgSecrets(this GitHubClient client, string orgName)
        {
            var rawResponse = await client.Connection.GetRaw(new Uri($"/orgs/{orgName}/actions/secrets", UriKind.Relative), new Dictionary<string, string>()
            {
                { "per_page", "100" }
            });

            var json = (string)rawResponse.HttpResponse.Body;
            var response = JsonSerializer.Deserialize<OrgSecretsResponse>(json);

            var result = new List<CachedOrgSecret>();

            if (response != null)
            {
                foreach (var secret in response.secrets)
                {
                    var cachedSecret = new CachedOrgSecret()
                    {
                        Name = secret.name,
                        CreatedAt = secret.created_at.ToLocalTime(),
                        UpdatedAt = secret.updated_at.ToLocalTime(),
                        Visibility = secret.visibility
                    };
                    result.Add(cachedSecret);
                }
            }

            return result.ToArray();
        }

        public static async Task<IReadOnlyList<string>> GetOrgSecretRepositories(this GitHubClient client, string orgName, string secretName)
        {
            var rawResponse = await client.Connection.GetRaw(new Uri($"/orgs/{orgName}/actions/secrets/{secretName}/repositories", UriKind.Relative), new Dictionary<string, string>()
            {
                { "per_page", "100" }
            });

            var json = (string)rawResponse.HttpResponse.Body;
            var response = JsonSerializer.Deserialize<RepoListResponse>(json);
            if (response is null)
                return Array.Empty<string>();

            return response.repositories.Select(r => r.name).ToArray();
        }

        public static async Task<IReadOnlyList<CachedRepoSecret>> GetRepoSecrets(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var rawResponse = await client.Connection.GetRaw(new Uri($"/repos/{owner}/{repo}/actions/secrets", UriKind.Relative), new Dictionary<string, string>()
                {
                    { "per_page", "100" }
                });

                var json = (string)rawResponse.HttpResponse.Body;
                return DeserializeSecrets(json);
            }
            catch (NotFoundException)
            {
                return Array.Empty<CachedRepoSecret>();
            }
        }

        private static IReadOnlyList<CachedRepoSecret> DeserializeSecrets(string json)
        {
            var response = JsonSerializer.Deserialize<RepoSecretsResponse>(json);

            var result = new List<CachedRepoSecret>();

            if (response != null)
            {
                foreach (var secret in response.secrets)
                {
                    var cachedSecret = new CachedRepoSecret()
                    {
                        Name = secret.name,
                        CreatedAt = secret.created_at.ToLocalTime(),
                        UpdatedAt = secret.updated_at.ToLocalTime(),
                    };
                    result.Add(cachedSecret);
                }
            }

            return result.ToArray();
        }

        public static async Task<IReadOnlyList<CachedRepoEnvironment>> GetRepoEnvironments(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var rawResponse = await client.Connection.GetRaw(new Uri($"/repos/{owner}/{repo}/environments", UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                var response = JsonSerializer.Deserialize<RepoEnvironmentResponse>(json);

                var result = new List<CachedRepoEnvironment>();

                if (response != null)
                {
                    foreach (var environment in response.environments)
                    {
                        var cachedEnvironment = new CachedRepoEnvironment()
                        {
                            Id = environment.id,
                            NodeId = environment.node_id,
                            Name = environment.name,
                            Url = environment.html_url,
                            CreatedAt = environment.created_at.ToLocalTime(),
                            UpdatedAt = environment.updated_at.ToLocalTime()
                        };
                        result.Add(cachedEnvironment);
                    }
                }

                return result.ToArray();
            }
            catch (NotFoundException)
            {
                return Array.Empty<CachedRepoEnvironment>();
            }
        }

        public static async Task<IReadOnlyList<CachedRepoSecret>> GetRepoEnvironmentSecrets(this GitHubClient client, int repositoryId, string environmentName)
        {
            var rawResponse = await client.Connection.GetRaw(new Uri($"/repositories/{repositoryId}/environments/{environmentName}/secrets", UriKind.Relative), new Dictionary<string, string>()
            {
                { "per_page", "100" }
            });
            var json = (string)rawResponse.HttpResponse.Body;
            return DeserializeSecrets(json);
        }

        public static async Task<CachedActionPermissions> GetActionPermissionsAsync(this GitHubClient client, string owner, string? repo = null)
        {
            var result = new CachedActionPermissions();

            var permissions = await client.GetInternalActionPermissionsAsync(owner, repo);
            if (permissions is not null)
            {
                result.Enabled = permissions.enabled;
                result.AllowedActions = ParseAllowedActions(permissions.allowed_actions);
            }

            if (result.AllowedActions == CachedRepoAllowedActions.Selected)
            {
                var selectedActions = await client.GetInternalAllowedActionsAsync(owner, repo);
                if (selectedActions is not null)
                {
                    result.GitHubOwnedAllowed = selectedActions.github_owned_allowed;
                    result.VerifiedAllowed = selectedActions.verified_allowed;
                    if (selectedActions.patterns_allowed is not null)
                        result.PatternsAllowed = selectedActions.patterns_allowed;
                }
            }

            static CachedRepoAllowedActions ParseAllowedActions(string? text)
            {
                return text switch
                {
                    "all" => CachedRepoAllowedActions.All,
                    "local_only" => CachedRepoAllowedActions.LocalOnly,
                    "selected" => CachedRepoAllowedActions.Selected,
                    _ => CachedRepoAllowedActions.Disabled
                };
            }

            return result;
        }

        private static async Task<ActionPermissionsResponse?> GetInternalActionPermissionsAsync(this GitHubClient client, string owner, string? repo)
        {
            try
            {
                var uri = repo is null
                    ? $"/orgs/{owner}/actions/permissions"
                    : $"/repos/{owner}/{repo}/actions/permissions";

                var rawResponse = await client.Connection.GetRaw(new Uri(uri, UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                return JsonSerializer.Deserialize<ActionPermissionsResponse>(json);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        private static async Task<ActionPermissionsSelectedActionsResponse?> GetInternalAllowedActionsAsync(this GitHubClient client, string owner, string? repo)
        {
            try
            {
                var uri = repo is null
                    ? $"/orgs/{owner}/actions/permissions/selected-actions"
                    : $"/repos/{owner}/{repo}/actions/permissions/selected-actions";

                var rawResponse = await client.Connection.GetRaw(new Uri(uri, UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                return JsonSerializer.Deserialize<ActionPermissionsSelectedActionsResponse>(json);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public static async Task<CachedFile[]> GetRepoWorkflowsAsync(this GitHubClient client, string owner, string repo)
        {
            var response = await client.GetInternalRepoWorkflowsAsync(owner, repo);
            if (response is null || response.workflows is null)
                return Array.Empty<CachedFile>();

            var files = new List<CachedFile>(response.workflows.Length);

            foreach (var workflow in response.workflows)
            {
                if (workflow is null ||
                    workflow.name is null ||
                    workflow.path is null ||
                    workflow.html_url is null)
                    continue;

                byte[]? data = null;

                try
                {
                    data = await client.Repository.Content.GetRawContent(owner, repo, workflow.path);
                }
                catch (Exception)
                {
                    // Ignore
                }

                if (data is null)
                    continue;

                var contents = Encoding.UTF8.GetString(data);
                var file = new CachedFile
                {
                    Name = workflow.name,
                    Url = workflow.html_url,
                    Contents = contents
                };
                files.Add(file);
            }

            return files.ToArray();
        }

        private static async Task<ActionWorkflowsResponse?> GetInternalRepoWorkflowsAsync(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var rawResponse = await client.Connection.GetRaw(new Uri($"/repos/{owner}/{repo}/actions/workflows", UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                return JsonSerializer.Deserialize<ActionWorkflowsResponse>(json);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

#pragma warning disable CS8618 // Serialized type

        private sealed class OrgSecretsResponse
        {
            public int total_count { get; set; }
            public OrgSecret[] secrets { get; set; }
        }

        private sealed class OrgSecret
        {
            public string name { get; set; }
            public DateTimeOffset created_at { get; set; }
            public DateTimeOffset updated_at { get; set; }
            public string visibility { get; set; }
        }

        private sealed class RepoSecretsResponse
        {
            public int total_count { get; set; }
            public RepoSecret[] secrets { get; set; }
        }

        private sealed class RepoSecret
        {
            public string name { get; set; }
            public DateTimeOffset created_at { get; set; }
            public DateTimeOffset updated_at { get; set; }
        }

        private sealed class RepoListResponse
        {
            public int total_count { get; set; }
            public RepoListItem[] repositories { get; set; }
        }

        private sealed class RepoListItem
        {
            public string name { get; set; }
        }

        private sealed class RepoEnvironmentResponse
        {
            public int total_count { get; set; }
            public RepoEnvironment[] environments { get; set; }
        }

        private sealed class RepoEnvironment
        {
            public int id { get; set; }
            public string node_id { get; set; }
            public string name { get; set; }
            public string url { get; set; }
            public string html_url { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
        }

#pragma warning restore CS8618

        private sealed class ActionPermissionsResponse
        {
            public bool enabled { get; set; }
            public string? allowed_actions { get; set; }
            public string? selected_actions_url { get; set; }
        }

        private sealed class ActionPermissionsSelectedActionsResponse
        {
            public bool github_owned_allowed { get; set; }
            public bool verified_allowed { get; set; }
            public string[]? patterns_allowed { get; set; }
        }

        private sealed class ActionWorkflowsResponse
        {
            public int total_count { get; set; }
            public Workflow[]? workflows { get; set; }
        }

        private sealed class Workflow
        {
            public int id { get; set; }
            public string? node_id { get; set; }
            public string? name { get; set; }
            public string? path { get; set; }
            public string? state { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string? url { get; set; }
            public string? html_url { get; set; }
            public string? badge_url { get; set; }
        }
    }
}
