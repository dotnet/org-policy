using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Octokit;

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
            var rateLimitText = rateLimit == null
                                    ? null
                                    : $" (Remaining API quota: {rateLimit.Remaining})";
            logWriter.WriteLine($"{text}...{rateLimitText}");
        }

        public static Task WaitForEnoughQuotaAsync(this GitHubClient client, TextWriter logWriter)
        {
            var rateLimit = client.GetLastApiInfo()?.RateLimit;

            if (rateLimit != null && rateLimit.Remaining <= 50)
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

        public static async Task<Readme> GetReadme(this GitHubClient client, string owner, string repo)
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

        public static async Task<GitHubCodeOfConduct> GetCodeOfConduct(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                var uri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/code_of_conduct");
                var response = await client.Connection.Get<GitHubCodeOfConduct>(uri, null, "application/vnd.github.scarlet-witch-preview+json");
                if (response.Body?.Body == null)
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

        public static async Task<RepositoryContent> GetContributing(this GitHubClient client, string owner, string repo)
        {
        retry:
            try
            {
                var profileUri = new Uri(client.Connection.BaseAddress, $"/repos/{owner}/{repo}/community/profile");
                var profileResponseRaw = await client.Connection.Get<Dictionary<string, object>>(profileUri, null, "application/vnd.github.black-panther-preview+json");

                if (profileResponseRaw.Body == null)
                    return null;

                if (!profileResponseRaw.Body.TryGetValue("files", out var filesRaw))
                    return null;

                var files = filesRaw as IDictionary<string, object>;
                if (files == null)
                    return null;

                if (!files.TryGetValue("contributing", out var contributingRaw))
                    return null;

                var contributing = contributingRaw as IDictionary<string, object>;
                if (contributing == null)
                    return null;

                if (!contributing.TryGetValue("html_url", out var htmlUrlRaw))
                    return null;

                var htmlUrl = htmlUrlRaw as string;
                if (htmlUrl == null)
                    return null;

                if (!contributing.TryGetValue("url", out var urlRaw))
                    return null;

                var url = urlRaw as string;
                if (url == null)
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
    }
}
