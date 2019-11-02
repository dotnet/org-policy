namespace Microsoft.DotnetOrg.DevOps
{
    public sealed class DevOpsArtifact
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DevOpsArtifactResource Resource { get; set; }
    }

}
