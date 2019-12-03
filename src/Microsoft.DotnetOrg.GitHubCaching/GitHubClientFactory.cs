using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Octokit;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public static class GitHubClientFactory
    {
        public static async Task<GitHubClient> CreateAsync(string token = null, string scopes = "public_repo, read:org")
        {
            var productInformation = new ProductHeaderValue(GetExeName());
            var client = new GitHubClient(productInformation);
            if (string.IsNullOrEmpty(token))
                token = await GetOrCreateTokenAsync(scopes);

            client.Credentials = new Credentials(token);
            return client;
        }

        private static async Task<string> GetOrCreateTokenAsync(string scopes)
        {
            var environmentToken = Environment.GetEnvironmentVariable("GITHUBTOKEN");
            if (!string.IsNullOrEmpty(environmentToken))
                return environmentToken;

            var tokenFileName = GetTokenFileName();
            if (File.Exists(tokenFileName))
            {
                var encryptedBytesText = File.ReadAllText(tokenFileName).Trim();
                var encryptedBytes = Convert.FromBase64String(encryptedBytesText);
                var textBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(textBytes);
                return text;
            }
            else
            {
                var token = await CreateTokenAsync(scopes, isRenewal: false);
                var tokenFileDirectory = Path.GetDirectoryName(tokenFileName);
                Directory.CreateDirectory(tokenFileDirectory);

                var textBytes = Encoding.UTF8.GetBytes(token);
                var encryptedBytes = ProtectedData.Protect(textBytes, null, DataProtectionScope.CurrentUser);
                var encryptedBytesText = Convert.ToBase64String(encryptedBytes);
                File.WriteAllText(tokenFileName, encryptedBytesText);

                return token;
            }
        }

        private static string GetExeName()
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            return Path.GetFileNameWithoutExtension(exePath);
        }

        private static string GetTokenFileName()
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileInfo.CompanyName, fileInfo.ProductName, "github-token.txt");
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

        private static string ReadText(string item)
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

        private static string ReadNonEmptyText(string item, Func<string> reader)
        {
            while (true)
            {
                Console.Write($"{item}: ");
                var result = reader().Trim();
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
}
