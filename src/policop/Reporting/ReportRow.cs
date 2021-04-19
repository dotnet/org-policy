using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal readonly struct ReportRow
    {
        public ReportRow(CachedRepo? repo = null, CachedTeam? team = null, CachedUser? user = null, CachedUserAccess? userAccess = null, CachedTeamAccess? teamAccess = null, CachedWhatIfPermission? whatIfPermission = null)
        {
            Repo = repo;
            Team = team;
            User = user;
            UserAccess = userAccess;
            TeamAccess = teamAccess;
            WhatIfPermission = whatIfPermission;
        }

        public CachedOrg? Org
        {
            get
            {
                if (Repo is not null) return Repo.Org;
                if (Team is not null) return Team.Org;
                if (User is not null) return User.Org;
                if (UserAccess is not null) return UserAccess.Org;
                if (TeamAccess is not null) return TeamAccess.Org;
                return null;
            }
        }

        public CachedRepo? Repo { get; }
        public CachedTeam? Team { get; }
        public CachedUser? User { get; }
        public CachedUserAccess? UserAccess { get; }
        public CachedTeamAccess? TeamAccess { get; }
        public CachedWhatIfPermission? WhatIfPermission { get; }
    }
}
