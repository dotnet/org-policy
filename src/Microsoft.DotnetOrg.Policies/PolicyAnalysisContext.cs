using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

namespace Microsoft.DotnetOrg.Policies
{
    public sealed class PolicyAnalysisContext
    {
        public PolicyAnalysisContext(CachedOrg org, OspoLinkSet linkSet)
        {
            Org = org;
            LinkSet = linkSet;
        }

        public CachedOrg Org { get; }
        public OspoLinkSet LinkSet { get; }
    }
}
