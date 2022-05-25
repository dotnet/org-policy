namespace Microsoft.DotnetOrg.GitHubCaching
{
    public readonly struct CachedAccessReason
    {
        public static CachedAccessReason FromOwner => new CachedAccessReason(isOwner: true, isCollaborator: false, null);
        public static CachedAccessReason FromCollaborator => new CachedAccessReason(isOwner: false, isCollaborator: true, null);
        public static CachedAccessReason FromTeam(CachedTeam team)
        {
            if (team is null)
                throw new ArgumentNullException(nameof(team));

            return new CachedAccessReason(isOwner: false, isCollaborator: false, team);
        }

        private CachedAccessReason(bool isOwner, bool isCollaborator, CachedTeam? team)
        {
            IsOwner = isOwner;
            IsCollaborator = isCollaborator;
            Team = team;
        }

        public bool IsOwner { get; }
        public bool IsCollaborator { get; }
        public CachedTeam? Team { get; }

        public override string ToString()
        {
            if (IsOwner)
                return "(Owner)";

            if (IsCollaborator)
                return "(Collaborator)";

            return Team!.GetFullName();
        }
    }
}
