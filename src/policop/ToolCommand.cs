using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal abstract class ToolCommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract void AddOptions(OptionSet options);
        public abstract Task ExecuteAsync();
    }
}
