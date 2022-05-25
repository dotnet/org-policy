namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class CachedFile
{
    public string Name { get; set; }
    public string Contents { get; set; }
    public string Url { get; set; }
}
#pragma warning restore CS8618