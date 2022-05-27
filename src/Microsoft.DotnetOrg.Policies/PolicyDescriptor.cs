namespace Microsoft.DotnetOrg.Policies;

public sealed class PolicyDescriptor
{
    public PolicyDescriptor(string diagnosticId, string title, PolicySeverity severity)
    {
        if (string.IsNullOrEmpty(diagnosticId))
            throw new ArgumentException($"'{nameof(diagnosticId)}' cannot be null or empty.", nameof(diagnosticId));

        if (string.IsNullOrEmpty(title))
            throw new ArgumentException($"'{nameof(title)}' cannot be null or empty.", nameof(title));

        DiagnosticId = diagnosticId;
        Title = title;
        Severity = severity;
    }

    public string DiagnosticId { get; }
    public string Title { get; }
    public PolicySeverity Severity { get; }
}