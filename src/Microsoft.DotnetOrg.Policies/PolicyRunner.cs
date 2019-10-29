using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotnetOrg.Policies
{
    public static class PolicyRunner
    {
        public static IReadOnlyList<PolicyRule> GetRules()
        {
            return typeof(PolicyRunner).Assembly
                                        .GetTypes()
                                        .Where(t => !t.IsAbstract &&
                                                    t.GetConstructor(Array.Empty<Type>()) != null &&
                                                    typeof(PolicyRule).IsAssignableFrom(t))
                                        .Select(t => Activator.CreateInstance(t))
                                        .Cast<PolicyRule>()
                                        .ToList();
        }

        public static IReadOnlyList<PolicyViolation> Run(PolicyAnalysisContext context)
        {
            var rules = GetRules();
            return Run(context, rules);
        }

        public static IReadOnlyList<PolicyViolation> Run(PolicyAnalysisContext context, IEnumerable<PolicyRule> rules)
        {
            return rules.SelectMany(r => r.GetViolations(context)).ToArray();
        }
    }
}
