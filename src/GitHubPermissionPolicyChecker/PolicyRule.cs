using System.Collections.Generic;

namespace GitHubPermissionPolicyChecker
{
    internal abstract class PolicyRule
    {
        public abstract IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context);
    }
}
