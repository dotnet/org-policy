namespace Microsoft.DotnetOrg.GitHubCaching;

public sealed class CachedRepoProperty
{
    public CachedRepoProperty(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string Value { get; }
}