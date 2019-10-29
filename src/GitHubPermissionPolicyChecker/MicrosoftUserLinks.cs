using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Terrajobst.Ospo;

namespace GitHubPermissionPolicyChecker
{
    public sealed class MicrosoftUserLinks
    {
        public MicrosoftUserLinks(IReadOnlyList<UserLink> links)
        {
            Links = links;
            LinkByGitHubLogin = links.ToDictionary(l => l.GitHubInfo.Login);
        }

        public IReadOnlyList<UserLink> Links { get; }
        public IReadOnlyDictionary<string, UserLink> LinkByGitHubLogin { get; }

        public static async Task<MicrosoftUserLinks> LoadAsync(OspoClient client)
        {
            var links = await client.GetAllAsync();
            return new MicrosoftUserLinks(links);
        }
    }
}
