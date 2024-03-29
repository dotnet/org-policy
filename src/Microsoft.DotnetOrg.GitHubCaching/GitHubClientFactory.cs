﻿using System.Diagnostics;
using System.Text;
using Octokit;

using QConnection = Octokit.GraphQL.Connection;
using QProductHeaderValue = Octokit.GraphQL.ProductHeaderValue;

namespace Microsoft.DotnetOrg.GitHubCaching;

public static class GitHubClientFactory
{
    public static async Task<GitHubClient> CreateAsync(string? token = null, string scopes = "public_repo, read:org")
    {
        if (string.IsNullOrEmpty(token))
            token = await GetOrCreateTokenAsync(scopes);

        return Create(token);
    }

    public static async Task<QConnection> CreateGraphAsync(string? token = null, string scopes = "public_repo, read:org")
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
            if (!await IsValidAsync(token))
            {
                Console.Error.WriteLine("error: GitHub token isn't valid anymore");
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

    private static async Task<bool> IsValidAsync(string token)
    {
        var client = Create(token);
        try
        {
            await client.User.Current();
            return true;
        }
        catch (AuthorizationException)
        {
            return false;
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
        var scopeList = scopes.Split(',').Select(s => s.Trim()).ToArray();

        Console.WriteLine($"This is the first time you run {header.Name}.");
        Console.WriteLine($"{header.Name} needs to access GitHub for {scopes}.");
        Console.WriteLine($"Let's log you in so it can create a personal access token.");
        Console.WriteLine($"This means you won't need to enter your credentials next");
        Console.WriteLine($"time it runs.");
        Console.WriteLine();

        var client = new GitHubClient(header);

        var authorization = new NewAuthorization(header.Name, scopeList)
        {
            Fingerprint = Guid.NewGuid().ToString()
        };

        while (true)
        {
            var userName = ReadNonEmptyText("user name");
            var password = ReadNonEmptyTextMasked("password");
            var twoFactorToken = ReadText("2FA token");

            client.Credentials = new Credentials(userName, password);

            try
            {
                var result = string.IsNullOrEmpty(twoFactorToken)
                    ? await client.Authorization.Create(authorization)
                    : await client.Authorization.Create(authorization, twoFactorToken);
                return result.Token;
            }
            catch (TwoFactorRequiredException)
            {
                Console.WriteLine("error: you need to provide a 2FA token");
            }
            catch (AuthorizationException)
            {
                Console.WriteLine("error: wrong user name or password");
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