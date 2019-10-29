using System.Collections.Generic;
using System.Linq;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class TooManyRepoAdmins : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.TooManyRepoAdmins;

        public override IEnumerable<PolicyViolation> GetViolations(CachedOrg org)
        {
            const int Threshold = 4;

            foreach (var repo in org.Repos)
            {
                var numberOfAdmins = repo.Users.Count(ua => ua.Permission == CachedPermission.Admin &&
                                                           !ua.Describe().IsOwner);

                if (numberOfAdmins > Threshold)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"Repo '{repo.Name}' has more than {Threshold} admins ({numberOfAdmins}). Reduce the number of admins.",
                        repo: repo
                    );
                }
            }
        }
    }
}
