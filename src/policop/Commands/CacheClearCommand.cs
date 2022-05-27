using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class CacheClearCommand : ToolCommand
{
    private bool _force;

    public override string Name => "cache-clear";

    public override string Description => "Clears the cached orgs";

    public override void AddOptions(OptionSet options)
    {
        options.Add("f", "Actually clears the cache", v => _force = true);
    }

    public override Task ExecuteAsync()
    {
        foreach (var file in CacheManager.GetOrgCaches())
        {
            Console.WriteLine($"rm: {file}");

            if (_force)
                file.Delete();
        }

        if (!_force && CacheManager.GetOrgCaches().Any())
        {
            Console.WriteLine("info: no files deleted");
            Console.WriteLine("info: to actually delete files, specify -f");
        }

        return Task.CompletedTask;
    }
}