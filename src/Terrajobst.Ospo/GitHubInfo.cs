using System.Collections.Generic;

namespace Terrajobst.Ospo
{
    public sealed class GitHubInfo
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public List<string> Organizations { get; set; }
    }
}
