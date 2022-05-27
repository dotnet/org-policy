namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
internal sealed class CachedTeamMember
{
    public string TeamSlug { get; set; }
    public string UserLogin { get; set; }
    public bool IsMaintainer { get; set; }
}
#pragma warning restore CS8618