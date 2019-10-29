using System.Collections.Generic;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker
{
    internal abstract class PolicyRule
    {
        public abstract IEnumerable<PolicyViolation> GetViolations(CachedOrg org);
    }
}
