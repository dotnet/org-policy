using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal static class OptionSetExtensions
    {
        public static OptionSet AddOrg(this OptionSet options, Action<string> action)
        {
            // If there is a single org, let's default to that org.
            var orgs = CacheManager.GetOrgCaches().ToArray();
            if (orgs.Length == 1)
                action(Path.GetFileNameWithoutExtension(orgs[0].Name));

            return options.Add("org=", "The {name} of the GitHub organization", action);
        }
    }
}
