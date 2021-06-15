
using System;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedActionPermissions
    {
        public bool Enabled { get; set; }
        public CachedRepoAllowedActions AllowedActions { get; set; }
        public bool GitHubOwnedAllowed { get; set; }
        public bool VerifiedAllowed { get; set; }
        public string[] PatternsAllowed { get; set; } = Array.Empty<string>();
    }
}
