using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.DevOps
{
    public static class DevOpsClientFactory
    {
        public static async Task<DevOpsClient> CreateAsync(string organization, string project, string token = null)
        {
            if (string.IsNullOrEmpty(token))
                token = await GetTokenAsync(organization, project);

            return new DevOpsClient(organization, project, token);
        }

        private static async Task<string> GetTokenAsync(string organization, string project)
        {
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
                var token = await CreateTokenAsync(organization, project);
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
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileInfo.CompanyName, fileInfo.ProductName, "dev-ops-token.txt");
        }

        private static Task<string> CreateTokenAsync(string organization, string project)
        {
            var productName = GetExeName();
            var url = $"https://{organization}.visualstudio.com/_usersSettings/tokens";

            Console.WriteLine($"This is the first time you run {productName}.");
            Console.WriteLine($"{productName} needs to access the Azure DevOps APIs.");
            Console.WriteLine();
            Console.WriteLine($"Let's log you in so it can create a personal access token.");
            Console.WriteLine();
            Console.WriteLine($"Press any key to navigate to {url}");
            Console.WriteLine($"and create a token with the scope: Build - Read.");

            Console.ReadKey(true);
            Console.WriteLine();

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            Console.Write("Enter token: ");
            var result = ReadPassword();

            return Task.FromResult(result);
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
