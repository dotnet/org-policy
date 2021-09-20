using Mono.Options;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ListTokensCommand : ToolCommand
    {
        public override string Name => "list-tokens";

        public override string Description => "Lists the stored access tokens.";

        public override void AddOptions(OptionSet options)
        {
        }

        public override Task ExecuteAsync()
        {
            var directory = GetTokenDirectory();
            if (Directory.Exists(directory))
            {
                foreach (var fileName in Directory.GetFiles(directory))
                    Console.WriteLine(fileName);
            }

            return Task.CompletedTask;
        }

        private static string GetTokenDirectory()
        {
            var exePath = Environment.GetCommandLineArgs()[0];
            var fileInfo = FileVersionInfo.GetVersionInfo(exePath)!;
            var companyName = fileInfo.CompanyName ?? string.Empty;
            var productName = fileInfo.ProductName ?? string.Empty;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), companyName, productName);
        }
    }
}
