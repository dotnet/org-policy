namespace Microsoft.DotnetOrg.DevOps;
#pragma warning disable CS8618 // This is a serialized type.
public sealed class DevOpsArtifact
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DevOpsArtifactResource Resource { get; set; }
}
#pragma warning restore CS8618