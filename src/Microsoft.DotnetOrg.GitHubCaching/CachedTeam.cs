using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedTeam
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string Description { get; set; }
        public bool IsSecret { get; set; }
        public SortedSet<string> MaintainerLogins { get; set; } = new SortedSet<string>();
        public SortedSet<string> MemberLogins { get; set; } = new SortedSet<string>();
        public List<CachedTeamAccess> Repos { get; set; } = new List<CachedTeamAccess>();

        [JsonIgnore]
        public CachedOrg Org { get; set; }

        [JsonIgnore]
        public string Url => CachedOrg.GetTeamUrl(Org.Name, Name);

        [JsonIgnore]
        public CachedTeam Parent { get; set; }

        [JsonIgnore]
        public List<CachedTeam> Children { get; } = new List<CachedTeam>();

        [JsonIgnore]
        public List<CachedUser> Maintainers { get; } = new List<CachedUser>();

        [JsonIgnore]
        public List<CachedUser> Members { get; } = new List<CachedUser>();

        public string GetFullName()
        {
            var teamNames = AncestorsAndSelf().Reverse().Select(t => t.Name);
            return string.Join("/", teamNames);
        }

        public IEnumerable<CachedTeam> AncestorsAndSelf()
        {
            var current = this;
            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        public IEnumerable<CachedTeam> DescendentsAndSelf()
        {
            var stack = new Stack<CachedTeam>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                foreach (var next in current.Children)
                    stack.Push(next);
            }
        }
    }
}
