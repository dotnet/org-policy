using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.Ospo
{
    public sealed class OspoLinkSet
    {
        public OspoLinkSet()
        {
        }

        public void Initialize()
        {
            LinkByLogin = Links.ToDictionary(l => l.GitHubInfo.Login);
        }

        public IReadOnlyList<OspoLink> Links { get; set; } = new List<OspoLink>();

        [JsonIgnore]
        public IReadOnlyDictionary<string, OspoLink> LinkByLogin { get; set; } = new Dictionary<string, OspoLink>();
    }
}
