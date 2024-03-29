﻿using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class CacheOrgCommand : ToolCommand
{
    private string? _orgName;
    private bool _includeLinks;

    public override string Name => "cache-org";

    public override string Description => "Downloads the organization data from GitHub";

    public override void AddOptions(OptionSet options)
    {
        options.AddOrg(v => _orgName = v)
               .Add("with-ms-links", "Include linking information to Microsoft users", v => _includeLinks = true);
    }

    public override async Task ExecuteAsync()
    {
        var orgNames = !string.IsNullOrEmpty(_orgName)
            ? new[] { _orgName }
            : CacheManager.GetCachedOrgNames().ToArray();

        var client = await GitHubClientFactory.CreateAsync();
        var connection = await GitHubClientFactory.CreateGraphAsync();
        var ospoClient = !_includeLinks ? null : await OspoClientFactory.CreateAsync();

        foreach (var orgName in orgNames)
        {
            var result = await CachedOrg.LoadAsync(client, connection, orgName, Console.Out, ospoClient);
            if (result is not null)
                await CacheManager.StoreOrgAsync(result);
        }
    }
}