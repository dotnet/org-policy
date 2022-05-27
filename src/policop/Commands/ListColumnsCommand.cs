using Microsoft.Csv;
using Microsoft.DotnetOrg.PolicyCop.Reporting;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class ListColumnsCommand : ToolCommand
{
    public override string Name => "list-columns";

    public override string Description => "Shows the list of available columns";

    public override void AddOptions(OptionSet options)
    {
    }

    public override Task ExecuteAsync()
    {
        var document = new CsvDocument("name", "description");
        using (var writer = document.Append())
        {
            foreach (var column in ReportColumn.All)
            {
                writer.Write(column.Name);
                writer.Write(column.Description);
                writer.WriteLine();
            }
        }

        document.PrintToConsole();
        return Task.CompletedTask;
    }
}