
using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker
{
    internal sealed class PolicyAnalysisContext
    {
        public PolicyAnalysisContext(CachedOrg org, MicrosoftUserLinks userLinks)
        {
            Org = org;
            UserLinks = userLinks;
        }

        public CachedOrg Org { get; }
        public MicrosoftUserLinks UserLinks { get; }
    }
}
