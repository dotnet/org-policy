namespace Microsoft.DotnetOrg.Policies;

public abstract class PolicyRule
{
    public abstract PolicyDescriptor Descriptor { get; }

    public virtual void GetViolations(PolicyAnalysisContext context)
    {
    }

    public virtual Task GetViolationsAsync(PolicyAnalysisContext context)
    {
        GetViolations(context);
        return Task.CompletedTask;
    }
}