using System.Collections.Generic;

namespace Microsoft.DotnetOrg.Policies
{
    public abstract class PolicyRule
    {
        public abstract IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context);
    }
}
