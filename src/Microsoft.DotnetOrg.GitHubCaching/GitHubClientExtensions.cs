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

        public static async Task<Readme?> GetReadme(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                return await client.Repository.Content.GetReadme(owner, repo);
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

        public static async Task<GitHubCodeOfConduct?> GetCodeOfConduct(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                var uri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/code_of_conduct");
                var response = await client.Connection.Get<GitHubCodeOfConduct>(uri, null, "application/vnd.github.scarlet-witch-preview+json");
                if (response.Body?.Body is null)
                    return null;

                // Make sure we have a sensible value
                var htmlUri = new Uri(response.Body.HtmlUrl);
                response.Body.Name = htmlUri.Segments.Last();

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

        public static async Task<RepositoryContent?> GetContributing(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                var profileUri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/profile");
                var profileResponseRaw = await client.Connection.Get<Dictionary<string, object>>(profileUri, null, "application/vnd.github.black-panther-preview+json");

                if (profileResponseRaw.Body is null)
                    return null;

                if (!profileResponseRaw.Body.TryGetValue("files", out var filesRaw))
                    return null;

                var files = filesRaw as IDictionary<string, object>;
                if (files is null)
                    return null;

                if (!files.TryGetValue("contributing", out var contributingRaw))
                    return null;

                var contributing = contributingRaw as IDictionary<string, object>;
                if (contributing is null)
                    return null;

                if (!contributing.TryGetValue("html_url", out var htmlUrlRaw))
                    return null;

                var htmlUrl = htmlUrlRaw as string;
                if (htmlUrl is null)
                    return null;

                if (!contributing.TryGetValue("url", out var urlRaw))
                    return null;

                var url = urlRaw as string;
                if (url is null)
                    return null;

                var uri = new Uri(url);
                var response = await client.Connection.GetResponse<RepositoryContent>(uri);
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
    }
}
