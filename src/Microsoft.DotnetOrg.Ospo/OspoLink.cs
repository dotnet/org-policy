using System.Text.Json.Serialization;

namespace Microsoft.DotnetOrg.Ospo
{
    public class OspoLink
    {
        [JsonPropertyName("github")]
        public GitHubInfo GitHubInfo { get; set; }

        [JsonPropertyName("aad")]
        public MicrosoftInfo MicrosoftInfo { get; set; }
    }
}
