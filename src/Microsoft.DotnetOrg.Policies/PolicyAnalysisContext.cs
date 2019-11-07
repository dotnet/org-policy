using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies
{
    public sealed class PolicyAnalysisContext
    {
        public PolicyAnalysisContext(CachedOrg org)
        {
            Org = org;
        }

        public CachedOrg Org { get; }
    }
}
