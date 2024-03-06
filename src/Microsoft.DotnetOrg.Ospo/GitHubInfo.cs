namespace Microsoft.DotnetOrg.Ospo;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class GitHubInfo
{
    public long Id { get; set; }
    public string Login { get; set; }
    public List<string> Organizations { get; set; }
}
#pragma warning restore CS8618