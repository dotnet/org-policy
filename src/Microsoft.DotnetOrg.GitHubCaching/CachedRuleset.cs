using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class CachedRuleset
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Target { get; set; }
    public string Enforcement { get; set; }
    public IReadOnlyList<string> IncludeRefs { get; set; }
    public IReadOnlyList<string> ExcludeRefs { get; set; }

    [JsonIgnore]
    public CachedRepo Repo { get; set; }

    public bool IsActive => Enforcement == "active";

    public bool Matches(string branchRef)
    {
        if (!IsActive)
            return false;

        if (Target != "branch")
            return false;

        // Check if the branch matches any include pattern
        bool included = IncludeRefs.Any(pattern => MatchesPattern(branchRef, pattern));

        // Check if the branch matches any exclude pattern
        bool excluded = ExcludeRefs.Any(pattern => MatchesPattern(branchRef, pattern));

        return included && !excluded;
    }

    private static bool MatchesPattern(string branchRef, string pattern)
    {
        // GitHub ruleset patterns can be:
        // - Exact ref: "refs/heads/main"
        // - Wildcard: "refs/heads/release/*"
        // - Default branch: "~DEFAULT_BRANCH"

        if (pattern == branchRef)
            return true;

        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            return branchRef.StartsWith(prefix + "/");
        }

        return false;
    }
}
#pragma warning restore CS8618
