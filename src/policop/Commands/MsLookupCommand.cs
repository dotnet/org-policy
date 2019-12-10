using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Csv;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class MsLookupCommand : ToolCommand
    {
        private readonly List<string> _terms = new List<string>();
        private bool _viewInExcel;

        public override string Name => "ms-lookup";

        public override string Description => "Looks up a Microsoft user by alias, email, name or GitHub handle";

        public override void AddOptions(OptionSet options)
        {
            options.Add("<>", v => _terms.Add(v))
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true);
        }

        public override async Task ExecuteAsync()
        {
            if (_terms.Count == 0)
            {
                Console.Error.WriteLine("error: needs argument");
                return;
            }

            var client = await OspoClientFactory.CreateAsync();
            var linkSet = await client.GetAllAsync();

            var document = new CsvDocument("GitHub", "Name", "Alias", "Email");

            using (var writer = document.Append())
            {
                foreach (var link in linkSet.Links)
                {
                    var anyMatch = _terms.Any(t => IsMatch(link, t));
                    if (!anyMatch)
                        continue;

                    writer.Write($"{link.GitHubInfo.Login}");
                    writer.Write($"{link.MicrosoftInfo.PreferredName}");
                    writer.Write($"{link.MicrosoftInfo.Alias}");
                    writer.Write($"{link.MicrosoftInfo.EmailAddress}");
                    writer.WriteLine();
                }
            }

            if (_viewInExcel)
                document.ViewInExcel();
            else
                document.PrintToConsole();
        }

        private static bool IsMatch(OspoLink link, string term)
        {
            var login = term.StartsWith("@")
                            ? term.Substring(1)
                            : term;

            var aliasText = term.Contains("@")
                                ? term.Substring(0, term.IndexOf('@'))
                                : term;

            return string.Equals(link.GitHubInfo.Id.ToString(), term, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(link.GitHubInfo.Login, login, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(link.MicrosoftInfo.Alias, aliasText, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(link.MicrosoftInfo.EmailAddress, term, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(link.MicrosoftInfo.PreferredName, term, StringComparison.OrdinalIgnoreCase);
        }
    }
}
