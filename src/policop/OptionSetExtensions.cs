using System;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal static class OptionSetExtensions
    {
        public static OptionSet AddOrg(this OptionSet options, Action<string> action)
        {
            return options.Add("org=", "The {name} of the GitHub organization", action);
        }
    }
}
