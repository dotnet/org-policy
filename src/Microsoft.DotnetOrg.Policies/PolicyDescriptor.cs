namespace Microsoft.DotnetOrg.Policies
{
    public sealed class PolicyDescriptor
    {
        public PolicyDescriptor(string diagnosticId, string title, PolicySeverity severity)
        {
            DiagnosticId = diagnosticId;
            Title = title;
            Severity = severity;
        }

        public string DiagnosticId { get; }
        public string Title { get; }
        public PolicySeverity Severity { get; }
    }
}
