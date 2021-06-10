using Octokit;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public static class GitHubClientExtensions
    {
        public static async Task PrintProgressAsync(this GitHubClient client, TextWriter logWriter, string task, string itemName, int itemIndex, int itemCount)
        {
            var percentage = (itemIndex + 1) / (float)itemCount;
            var text = $"{task}: {itemName} {percentage:P1}";
            await client.PrintProgressAsync(logWriter, text);
        }

        public static async Task PrintProgressAsync(this GitHubClient client, TextWriter logWriter, string text)
        {
            await client.WaitForEnoughQuotaAsync(logWriter);

            var rateLimit = client.GetLastApiInfo()?.RateLimit;
            var rateLimitText = rateLimit is null
                                    ? null
                                    : $" (Remaining API quota: {rateLimit.Remaining})";
            logWriter.WriteLine($"{text}...{rateLimitText}");
        }

        public static Task WaitForEnoughQuotaAsync(this GitHubClient client, TextWriter logWriter)
        {
            var rateLimit = client.GetLastApiInfo()?.RateLimit;

            if (rateLimit is not null && rateLimit.Remaining <= 50)
            {
                var padding = TimeSpan.FromMinutes(2);
                var waitTime = (rateLimit.Reset - DateTimeOffset.Now).Add(padding);
                if (waitTime > TimeSpan.Zero)
                {
                    logWriter.WriteLine($"API rate limit exceeded. Waiting {waitTime.TotalMinutes:N0} minutes until it resets ({rateLimit.Reset.ToLocalTime():M/d/yyyy h:mm tt}).");
                    return Task.Delay(waitTime);
                }
            }

            return Task.CompletedTask;
        }

        public static async Task<GitHubCommunityProfile?> GetCommunityProfile(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                var uri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/profile");
                var response = await client.Connection.Get<GitHubCommunityProfile>(uri, null, "application/vnd.github.scarlet-witch-preview+json");
                return response.Body;
            }
            catch (AbuseException ex)
            {
                await ex.HandleAsync();
                goto retry;
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

        retry:
            try
            {
                var uri = new Uri(file.RawUrl);
                var response = await client.Connection.Get<string>(uri, null, null);
                return response.Body;
            }
            catch (AbuseException ex)
            {
                await ex.HandleAsync();
                goto retry;
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public static async Task HandleAsync(this AbuseException exception)
        {
            var retrySeconds = exception.RetryAfterSeconds ?? 120;
            Console.WriteLine($"Abuse detection triggered. Waiting for {retrySeconds} seconds before retrying.");

            var delay = TimeSpan.FromSeconds(retrySeconds);
            await Task.Delay(delay);
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

        public static async Task<CachedRepoActionPermissions> GetRepoActionPermissionsAsync(this GitHubClient client, string owner, string repo)
        {
            var result = new CachedRepoActionPermissions();

            var permissions = await client.GetInternalRepoActionPermissionsAsync(owner, repo);
            if (permissions is not null)
            {
                result.Enabled = permissions.enabled;
                result.AllowedActions = ParseAllowedActions(permissions.allowed_actions);
            }

            if (result.AllowedActions == CachedRepoAllowedActions.Selected)
            {
                var selectedActions = await client.GetInternalRepoAllowedActionsAsync(owner, repo);
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

        private static async Task<RepoActionPermissionsResponse?> GetInternalRepoActionPermissionsAsync(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var rawResponse = await client.Connection.GetRaw(new Uri($"/repos/{owner}/{repo}/actions/permissions", UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                return JsonSerializer.Deserialize<RepoActionPermissionsResponse>(json);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        private static async Task<RepoActionPermissionsSelectedActionsResponse?> GetInternalRepoAllowedActionsAsync(this GitHubClient client, string owner, string repo)
        {
            try
            {
                var rawResponse = await client.Connection.GetRaw(new Uri($"/repos/{owner}/{repo}/actions/permissions/selected-actions", UriKind.Relative), new Dictionary<string, string>());
                var json = (string)rawResponse.HttpResponse.Body;
                return JsonSerializer.Deserialize<RepoActionPermissionsSelectedActionsResponse>(json);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

#pragma warning disable CS8618 // Serialized type

        private class OrgSecretsResponse
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

        private class RepoSecretsResponse
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

        private class RepoListResponse
        {
            public int total_count { get; set; }
            public RepoListItem[] repositories { get; set; }
        }

        private class RepoListItem
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

        private sealed class RepoActionPermissionsResponse
        {
            public bool enabled { get; set; }
            public string? allowed_actions { get; set; }
            public string? selected_actions_url { get; set; }
        }

        private sealed class RepoActionPermissionsSelectedActionsResponse
        {
            public bool github_owned_allowed { get; set; }
            public bool verified_allowed { get; set; }
            public string[]? patterns_allowed { get; set; }
        }
    }
}
