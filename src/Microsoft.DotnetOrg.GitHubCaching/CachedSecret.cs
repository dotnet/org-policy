using System;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // Serialized type
    public abstract class CachedSecret
    {
        public string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
#pragma warning restore CS8618
}
