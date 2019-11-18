using System;
using System.IO;
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
    }
}
