using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies
{
    public sealed class PolicyAnalysisContext
    {
        private readonly ConcurrentBag<PolicyViolation> _violations = new ConcurrentBag<PolicyViolation>();

        public PolicyAnalysisContext(CachedOrg org)
        {
            if (org is null)
                throw new ArgumentNullException(nameof(org));

            Org = org;
        }

        public CachedOrg Org { get; }

        public void ReportViolation(PolicyDescriptor descriptor,
                                    string title,
                                    string body,
                                    CachedRepo? repo = null,
                                    CachedSecret? secret = null,
                                    CachedBranch? branch = null,
                                    CachedTeam? team = null,
                                    CachedUser? user = null,
                                    IReadOnlyCollection<CachedUser>? assignees = null)
        {
            var violation = new PolicyViolation(descriptor,
                                                title,
                                                body,
                                                Org,
                                                repo,
                                                secret,
                                                branch,
                                                team,
                                                user,
                                                assignees);
            _violations.Add(violation);
        }

        public IReadOnlyList<PolicyViolation> GetViolations()
        {
            return _violations.ToArray();
        }
    }
}
