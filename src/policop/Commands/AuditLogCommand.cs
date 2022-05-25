using System.Text.RegularExpressions;
using Microsoft.Csv;
using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Newtonsoft.Json.Linq;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class AuditLogCommand : ToolCommand
    {
        private string? _orgName;
        private bool _viewInExcel;
        private string? _outputFileName;
        private readonly List<string> _phrases = new();

        public override string Name => "audit-log";

        public override string Description => "Searches the GitHub Audit Log";

        public override void AddOptions(OptionSet options)
        {
            options.AddOrg(v => _orgName = v)
                   .Add("excel", "Shows the results in Excel", v => _viewInExcel = true)
                   .Add("o|output=", "The {path} where the output .csv file should be written to.", v => _outputFileName = v)
                   .Add("<>", v => _phrases.Add(v));
        }

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(_orgName))
            {
                Console.Error.WriteLine($"error: --org must be specified");
                return;
            }

            if (_viewInExcel && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: --excel is only valid if Excel is installed.");
                return;
            }

            var client = await GitHubClientFactory.CreateAsync();
            var phrase = string.Join(" ", _phrases);
            var response = await client.Connection.GetRaw(new Uri($"/orgs/{_orgName}/audit-log", UriKind.Relative), new Dictionary<string, string>()
            {
                { "phrase", phrase},
                { "include", "all" }
            });

            var array = JArray.Parse((string)response.HttpResponse.Body);

            var keys = array.Cast<JObject>()
                            .SelectMany(o => o.Properties().Select(p => p.Name))
                            .Distinct()
                            .ToArray();

            var document = new CsvDocument(keys);
            using (var writer = document.Append())
            {
                foreach (var o in array.Cast<JObject>())
                {
                    foreach (var key in keys)
                    {
                        if (!o.TryGetValue(key, out var value))
                        {
                            writer.Write("");
                        }
                        else if (key == "@timestamp" || key == "created_at")
                        {
                            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)value);
                            writer.Write(timestamp.ToString());
                        }
                        else
                        {
                            writer.Write(Regex.Replace(value.ToString(), $"\\s*\\n\\s*", " "));
                        }
                    }

                    writer.WriteLine();
                }
            }

            if (_outputFileName is not null)
                document.Save(_outputFileName);
            else if (_viewInExcel)
                document.ViewInExcel();
            else
                document.PrintToConsole();
        }
    }
}
