namespace Microsoft.DotnetOrg.GitHubCaching
{
    internal sealed class CachedTeamMember
    {
        public string TeamSlug { get; internal set; }
        public string UserLogin { get; set; }
        public bool IsMaintainer { get; set; }
    }
}
