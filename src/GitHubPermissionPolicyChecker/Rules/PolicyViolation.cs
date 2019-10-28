using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker.Rules
{
    internal sealed class PolicyViolation
    {
        public PolicyViolation(string message, CachedRepo repo = null, string user = null, CachedTeam team = null)
        {
            Message = message;
            Repo = repo;
            User = user;
            Team = team;
        }

        public string Message { get; }
        public CachedRepo Repo { get; }
        public string User { get; }
        public CachedTeam Team { get; }
    }
}
