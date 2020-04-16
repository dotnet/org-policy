using System.Threading.Tasks;

using Microsoft.Csv;
using Microsoft.DotnetOrg.Policies;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands
{
    internal sealed class ListRulesCommand : ToolCommand
    {
        public override string Name => "list-rules";

        public override string Description => "Shows the list of policy rules";

        public override void AddOptions(OptionSet options)
        {
        }

        public override Task ExecuteAsync()
        {
            var document = new CsvDocument("id", "severity", "title");
            using (var writer = document.Append())
            {          
                foreach (var rule in PolicyRunner.GetRules())
                {
                    writer.Write(rule.Descriptor.DiagnosticId);
                    writer.Write(rule.Descriptor.Severity.ToString());
                    writer.Write(rule.Descriptor.Title);
                    writer.WriteLine();
                }
            }

            document.PrintToConsole();
            return Task.CompletedTask;
        }
    }

}
