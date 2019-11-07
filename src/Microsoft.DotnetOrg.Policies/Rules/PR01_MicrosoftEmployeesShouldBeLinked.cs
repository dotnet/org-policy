using System.Collections.Generic;

namespace Microsoft.DotnetOrg.Policies.Rules
{
    internal sealed class PR01_MicrosoftEmployeesShouldBeLinked : PolicyRule
    {
        public static PolicyDescriptor Descriptor { get; } = new PolicyDescriptor(
            "PR01",
            "Microsoft employees should be linked",
            PolicySeverity.Error
        );

        public override IEnumerable<PolicyViolation> GetViolations(PolicyAnalysisContext context)
        {
            foreach (var user in context.Org.Users)
            {
                var userClaimsToBeWorkingForMicrosoft = user.IsClaimingToBeWorkingForMicrosoft();
                var isMicrosoftUser = user.IsMicrosoftUser();

                if (userClaimsToBeWorkingForMicrosoft && !isMicrosoftUser)
                {
                    yield return new PolicyViolation(
                        Descriptor,
                        title: $"Microsoft-user '{user.Login}' should be linked",
                        body: $@"
                            User {user.Markdown()} appears to be a Microsoft employee. They should be [linked](https://opensource.microsoft.com/link) to a Microsoft account.

                            For more details, see [documentation](https://docs.opensource.microsoft.com/tools/github/accounts/linking.html).
                        ",
                        org: context.Org,
                        user: user
                    );
                }
            }
        }
    }
}
