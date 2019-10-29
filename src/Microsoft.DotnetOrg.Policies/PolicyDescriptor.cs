namespace Microsoft.DotnetOrg.Policies
{
    // You can rename policy descriptors, but you must not reorder them.
    //
    // Otherwise, it will break fingerprinting of violations.

    public enum PolicyDescriptor
    {
        MicrosoftEmployeesShouldBeLinked,
        MicrosoftOwnedRepoShouldOnlyGrantReadAccessToExternals,
        MicrosoftOwnedTeamShouldOnlyContainEmployees,
        MicrosoftTeamShouldBeMarkedAsOwnedByMicrosoft,
        MicrosoftTeamShouldOnlyGrantReadAccess,
        InactiveReposShouldBeArchived,
        UnusedTeamShouldBeRemoved,
        TooManyRepoAdmins,
        TooManyTeamMaintainers
    }
}
