using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.GitHubCaching
{
#pragma warning disable CS8618 // This is a serialized type.
    public sealed class CachedBranchProtectionRule
    {
        public bool DismissesStaleReviews { get; set; }
        public bool IsAdminEnforced { get; set; }
        public string Pattern { get; set; }
        public int? RequiredApprovingReviewCount { get; set; }
        public IReadOnlyList<string> RequiredStatusCheckContexts { get; set; }
        public bool RequiresApprovingReviews { get; set; }
        public bool RequiresCodeOwnerReviews { get; set; }
        public bool RequiresCommitSignatures { get; set; }
        public bool RequiresStatusChecks { get; set; }
        public bool RequiresStrictStatusChecks { get; set; }
        public bool RestrictsPushes { get; set; }
        public bool RestrictsReviewDismissals { get; set; }
        public IReadOnlyList<string> MatchingRefs { get; set; }
        
        [JsonIgnore]
        public CachedRepo Repo { get; set; }
    }
#pragma warning restore
}
