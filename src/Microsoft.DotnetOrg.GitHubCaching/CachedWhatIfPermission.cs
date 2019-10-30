namespace Microsoft.DotnetOrg.GitHubCaching
{
    public struct CachedWhatIfPermission
    {
        public CachedWhatIfPermission(CachedUserAccess userAccess, CachedPermission? newPermissions)
        {
            UserAccess = userAccess;
            NewPermissions = newPermissions;
        }

        public CachedUserAccess UserAccess { get; }
        public CachedPermission? NewPermissions { get; }

        public bool IsUnchanged => NewPermissions != null && UserAccess.Permission == NewPermissions.Value;

        public override string ToString()
        {
            var currentPermission = UserAccess.Permission.ToString().ToLower();
            var newPermissions = NewPermissions == null
                                    ? "no access"
                                    : NewPermissions.Value.ToString().ToLower();

            if (NewPermissions == null || UserAccess.Permission > NewPermissions.Value)
                return $"Downgraded from '{currentPermission}' to '{newPermissions}'";
            else if (UserAccess.Permission == NewPermissions.Value)
                return "Unchanged";
            else
                return $"Upgraded from '{currentPermission}' to '{newPermissions}'";
        }
    }

}
