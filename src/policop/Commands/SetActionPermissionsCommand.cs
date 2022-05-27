using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class SetActionPermissionsCommand : ToolCommand
{
    private string? _orgName;
    private string? _repoName;
    private bool? _enable;

    public override string Name => "set-action-permissions";

    public override string Description => "Enables or disables GitHub actions for a given repo.";

    public override void AddOptions(OptionSet options)
    {
        options.AddOrg(v => _orgName = v)
               .Add("r=", "Specifies the repo", v => _repoName = v)
               .Add("enable", "Enables actions", v => _enable = true)
               .Add("disable", "Disables actions", v => _enable = false);
    }

    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrEmpty(_orgName))
        {
            Console.Error.WriteLine($"error: --org must be specified");
            return;
        }

        if (string.IsNullOrEmpty(_repoName))
        {
            Console.Error.WriteLine($"error: -r must be specified");
            return;
        }

        if (_enable is null)
        {
            Console.Error.WriteLine($"error: either --enable or --disable must be specified");
            return;
        }

        var client = await GitHubClientFactory.CreateAsync();

        try
        {
            await client.Connection.Put<object>(new Uri($"/repos/{_orgName}/{_repoName}/actions/permissions", UriKind.Relative), new
            {
                enabled = _enable.Value
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
        }
    }
}