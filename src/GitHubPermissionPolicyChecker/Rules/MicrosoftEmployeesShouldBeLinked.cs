using System.Collections.Generic;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class MicrosoftEmployeesShouldBeLinked : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = PolicyDescriptor.MicrosoftEmployeesShouldBeLinked;

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var user in context.Org.Users)
            {
                var userClaimsToBeWorkingForMicrosoft = user.IsClaimingToBeWorkingForMicrosoft();
                var isMicrosoftUser = context.IsMicrosoftUser(user);

                if (userClaimsToBeWorkingForMicrosoft && !isMicrosoftUser)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        $"User '{user.Login}' seems to be a Microsoft employee. They should be linked to a Microsoft account.",
                        user: user
                    );
                }
            }
        }
    }
}
