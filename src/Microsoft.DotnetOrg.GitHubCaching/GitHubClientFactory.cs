using System.Diagnostics;
using System.Text;
using Octokit;

using QConnection = Octokit.GraphQL.Connection;
using QProductHeaderValue = Octokit.GraphQL.ProductHeaderValue;

namespace Microsoft.DotnetOrg.GitHubCaching;

public static class GitHubClientFactory
{
    private const string DefaultScopes = "repo, read:org";

    public static async Task<GitHubClient> CreateAsync(string? token = null, string scopes = DefaultScopes)
    {
        if (string.IsNullOrEmpty(token))
            token = await GetOrCreateTokenAsync(scopes);

        return Create(token);
    }

    public static async Task<QConnection> CreateGraphAsync(string? token = null, string scopes = DefaultScopes)
    {
        if (string.IsNullOrEmpty(token))
            token = await GetOrCreateTokenAsync(scopes);

        var productInformation = new QProductHeaderValue(GetExeName());
        var connection = new QConnection(productInformation, token);

        return connection;
    }

    private static async Task<string> GetOrCreateTokenAsync(string scopes)
    {
        var environmentToken = Environment.GetEnvironmentVariable("GITHUBTOKEN");
        if (!string.IsNullOrEmpty(environmentToken))
            return environmentToken;

        string? token = null;

        var tokenFileName = GetTokenFileName();
        if (File.Exists(tokenFileName))
        {
            token = File.ReadAllText(tokenFileName).Trim();
            var (isValid, hasScopes) = await IsValidAsync(token, scopes);

            if (!isValid)
            {
                Console.Error.WriteLine("error: GitHub token isn't valid anymore");
                token = null;
            }
            else if (!hasScopes)
            {
                Console.Error.WriteLine($"error: GitHub token doesn't have required scopes: {scopes}");
                Console.Error.WriteLine("       Please create a new token with the required scopes");
                token = null;
            }
        }

        if (token is null)
        {
            token = await CreateTokenAsync(scopes, isRenewal: false);
            var tokenFileDirectory = Path.GetDirectoryName(tokenFileName)!;
            Directory.CreateDirectory(tokenFileDirectory);
            File.WriteAllText(tokenFileName, token);
        }

        return token;
    }

    private static async Task<(bool isValid, bool hasRequiredScopes)> IsValidAsync(string token, string requiredScopes)
    {
        var client = Create(token);
        try
        {
            // Make a test API call to validate the token
            var response = await client.Connection.Get<object>(new Uri("/user", UriKind.Relative), null, null);

            // Check if the token has required scopes
            if (response.HttpResponse.Headers.TryGetValue("X-OAuth-Scopes", out var scopeHeader))
            {
                var tokenScopes = scopeHeader.Split(',').Select(s => s.Trim()).ToHashSet();
                var required = requiredScopes.Split(',').Select(s => s.Trim());

                bool hasAllScopes = required.All(tokenScopes.Contains);
                return (true, hasAllScopes);
            }

            // If we can't check scopes, assume they're valid
            return (true, true);
        }
        catch (AuthorizationException)
        {
            return (false, false);
        }
    }

    private static GitHubClient Create(string token)
    {
        var productInformation = new ProductHeaderValue(GetExeName());
        var client = new GitHubClient(productInformation)
        {
            Credentials = new Credentials(token)
        };

        return client;
    }

    private static string GetExeName()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        return Path.GetFileNameWithoutExtension(exePath);
    }

    private static string GetTokenFileName()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        var fileInfo = FileVersionInfo.GetVersionInfo(exePath)!;
        var companyName = fileInfo.CompanyName ?? string.Empty;
        var productName = fileInfo.ProductName ?? string.Empty;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), companyName, productName, "github-token.txt");
    }

    private static async Task<string> CreateTokenAsync(string scopes, bool isRenewal)
    {
        var header = new ProductHeaderValue(GetExeName());

        Console.WriteLine($"This is the first time you run {header.Name}.");
        Console.WriteLine($"{header.Name} needs to access GitHub for {scopes}.");
        Console.WriteLine();
        Console.WriteLine("Please create a Personal Access Token (classic) with the following scopes:");
        Console.WriteLine($"  {scopes}");
        Console.WriteLine();
        Console.WriteLine("To create a token:");
        Console.WriteLine("1. Go to: https://github.com/settings/tokens/new");
        Console.WriteLine("2. Give it a descriptive name (e.g., 'policop')");
        Console.WriteLine($"3. Select the following scopes: {scopes}");
        Console.WriteLine("4. Click 'Generate token'");
        Console.WriteLine("5. Copy the token and paste it below");
        Console.WriteLine();
        Console.WriteLine("Note: You can also set the GITHUBTOKEN environment variable to skip this prompt.");
        Console.WriteLine();

        while (true)
        {
            var token = ReadNonEmptyText("GitHub Personal Access Token");

            // Validate the token format (should start with ghp_ for classic PATs or github_pat_ for fine-grained PATs)
            if (!token.StartsWith("ghp_") && !token.StartsWith("github_pat_"))
            {
                Console.WriteLine("error: The token doesn't appear to be a valid GitHub Personal Access Token.");
                Console.WriteLine("       Classic tokens start with 'ghp_' and fine-grained tokens start with 'github_pat_'");
                continue;
            }

            // Validate the token works and has required scopes
            var (isValid, hasScopes) = await IsValidAsync(token, scopes);

            if (!isValid)
            {
                Console.WriteLine("error: The token is invalid or authentication failed.");
                Console.WriteLine("       Please check the token and try again.");
            }
            else if (!hasScopes)
            {
                Console.WriteLine($"error: The token doesn't have the required scopes: {scopes}");
                Console.WriteLine("       Please create a new token with the required scopes.");
            }
            else
            {
                Console.WriteLine("Token validated successfully!");
                return token;
            }
        }
    }

    private static string? ReadText(string item)
    {
        Console.Write($"{item}: ");
        return Console.ReadLine();
    }

    private static string ReadNonEmptyText(string item)
    {
        return ReadNonEmptyText(item, Console.ReadLine);
    }

    private static string ReadNonEmptyTextMasked(string item)
    {
        return ReadNonEmptyText(item, ReadPassword);
    }

    private static string ReadNonEmptyText(string item, Func<string?> reader)
    {
        while (true)
        {
            Console.Write($"{item}: ");
            var result = (reader() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(result))
                return result;

            Console.WriteLine($"error: '{item}' is required.");
        }
    }

    private static string ReadPassword()
    {
        var pwd = new StringBuilder();
        while (true)
        {
            var i = Console.ReadKey(true);
            if (i.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            else if (i.Key == ConsoleKey.Backspace)
            {
                if (pwd.Length > 0)
                {
                    pwd.Remove(pwd.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (i.KeyChar != '\u0000')
            {
                pwd.Append(i.KeyChar);
                Console.Write("*");
            }
        }
        return pwd.ToString();
    }
}