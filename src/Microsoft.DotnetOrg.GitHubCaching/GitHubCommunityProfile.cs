namespace Microsoft.DotnetOrg.GitHubCaching;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class GitHubCommunityProfile
{
    public int HealthPercentage { get; set; }
    public string Description { get; set; }
    public string Documentation { get; set; }
    public CommunityFiles Files { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool ContentReportsEnabled { get; set; }

    public sealed class CommunityFiles
    {
        public CommunityFile CodeOfConductFile { get; set; }
        public CommunityFile Contributing { get; set; }
        public LicenseFile License { get; set; }
        public CommunityFile Readme { get; set; }
    }

    public class CommunityFile
    {
        public string Url { get; set; }
        public string HtmlUrl { get; set; }

        public string FileName => Path.GetFileName(new Uri(HtmlUrl).AbsolutePath);
        public string RawUrl
        {
            get
            {
                var builder = new UriBuilder(HtmlUrl) { Host = "raw.githubusercontent.com" };
                builder.Path = builder.Path.Replace("/blob/", "/", StringComparison.Ordinal);
                return builder.ToString();
            }
        }
    }

    public sealed class LicenseFile : CommunityFile
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string SpdxId { get; set; }
        public string NodeId { get; set; }
    }
}

#pragma warning restore CS8618