using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.DotnetOrg.Ospo;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedUser
    {
        public string Login { get; set; }
        public string Name { get; set; }
        public string Company { get; set; }
        public string Email { get; set; }
        public bool IsOwner { get; set; }
        public bool IsMember { get; set; }
        public MicrosoftInfo MicrosoftInfo { get; set; }

        [JsonIgnore]
        public CachedOrg Org { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetUserUrl(Login);

        [JsonIgnore]
        public bool IsExternal => !IsMember;

        [JsonIgnore]
        public List<CachedTeam> Teams { get; } = new List<CachedTeam>();

        [JsonIgnore]
        public List<CachedUserAccess> Repos { get; } = new List<CachedUserAccess>();
    }
}
