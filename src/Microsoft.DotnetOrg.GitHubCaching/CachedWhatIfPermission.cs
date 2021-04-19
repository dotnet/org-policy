namespace Microsoft.DotnetOrg.GitHubCaching
{
    public readonly struct CachedWhatIfPermission
    {
        public CachedWhatIfPermission(CachedUserAccess userAccess, CachedPermission? newPermissions)
        {
            UserAccess = userAccess;
            NewPermissions = newPermissions;
        }

        public CachedUserAccess UserAccess { get; }
        public CachedPermission? NewPermissions { get; }

        public bool IsUnchanged => NewPermissions is not null && UserAccess.Permission == NewPermissions.Value;

        public override string ToString()
        {
            if (UserAccess.Permission == NewPermissions)
                return "(unchanged)";

            var oldPermission = UserAccess.Permission.ToString().ToLower();
            var newPermission = NewPermissions?.ToString().ToLower() ?? "(no access)";
            return $"{oldPermission} -> {newPermission}";
        }
    }
}
