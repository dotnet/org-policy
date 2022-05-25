namespace Microsoft.DotnetOrg.Policies
{
    public static class PolicyRunner
    {
        public static IReadOnlyList<PolicyRule> GetRules()
        {
            return typeof(PolicyRunner).Assembly
                                        .GetTypes()
                                        .Where(t => !t.IsAbstract &&
                                                    t.GetConstructor(Array.Empty<Type>()) is not null &&
                                                    typeof(PolicyRule).IsAssignableFrom(t))
                                        .Select(t => Activator.CreateInstance(t))
                                        .Cast<PolicyRule>()
                                        .ToList();
        }

        public static Task RunAsync(PolicyAnalysisContext context)
        {
            var rules = GetRules().Where(r => r.Descriptor.Severity > PolicySeverity.Hidden);
            return RunAsync(context, rules);
        }

        public static Task RunAsync(PolicyAnalysisContext context, IEnumerable<PolicyRule> rules)
        {
            var ruleTasks = rules.Select(r => r.GetViolationsAsync(context));
            return Task.WhenAll(ruleTasks);
        }
    }
}
