using System.Text.Json.Serialization;

namespace Terrajobst.Ospo
{
    public class UserLink
    {
        [JsonPropertyName("github")]
        public GitHubInfo GitHubInfo { get; set; }

        [JsonPropertyName("aad")]
        public MicrosoftInfo MicrosoftInfo { get; set; }
    }
}
