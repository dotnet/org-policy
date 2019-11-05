using Microsoft.DotnetOrg.GitHubCaching;
using Microsoft.DotnetOrg.Ospo;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal struct ReportRow
    {
        public ReportRow(CachedRepo repo = null, CachedTeam team = null, CachedUser user = null, OspoLinkSet linkSet = null, CachedUserAccess userAccess = null, CachedTeamAccess teamAccess = null, CachedWhatIfPermission? whatIfPermission = null)
        {
            Repo = repo;
            Team = team;
            User = user;
            LinkSet = linkSet;
            UserAccess = userAccess;
            TeamAccess = teamAccess;
            WhatIfPermission = whatIfPermission;
        }

        public CachedRepo Repo { get; }
        public CachedTeam Team { get; }
        public CachedUser User { get; }
        public OspoLinkSet LinkSet { get; }
        public CachedUserAccess UserAccess { get; }
        public CachedTeamAccess TeamAccess { get; }
        public CachedWhatIfPermission? WhatIfPermission { get; }
    }
}
