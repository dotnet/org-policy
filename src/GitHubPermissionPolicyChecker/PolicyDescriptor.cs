namespace GitHubPermissionPolicyChecker
{
    // You can rename policy descriptors, but you must not reorder them.
    //
    // Otherwise, it will break fingerprinting of violations.

    internal enum PolicyDescriptor
    {
        MicrosoftEmployeesShouldBeInMicrosoftTeam,
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
