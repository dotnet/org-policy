namespace Microsoft.DotnetOrg.Policies
{
    // You can rename policy descriptors, but you must not reorder them.
    //
    // Otherwise, it will break fingerprinting of violations.

    public enum PolicyDescriptor
    {
        MicrosoftEmployeesShouldBeLinked,
        MicrosoftOwnedRepoShouldOnlyGrantPullAccessToExternals,
        MicrosoftOwnedTeamShouldOnlyContainEmployees,
        MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft,
        MicrosoftTeamShouldOnlyGrantPullAccess,
        InactiveReposShouldBeArchived,
        UnusedTeamShouldBeRemoved,
        TooManyRepoAdmins,
        TooManyTeamMaintainers
    }
}
