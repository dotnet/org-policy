using System;

namespace Microsoft.DotnetOrg.DevOps
{
#pragma warning disable CS8618 // This is a serialized type.
    public sealed class DevOpsBuild
    {
        public int Id { get; set; }
        public string BuildNumber { get; set; }
        public string Status { get; set; }
        public string Result { get; set; }
        public DateTimeOffset QueueTime { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset FinishTime { get; set; }
        public Uri Url { get; set; }
    }
#pragma warning restore CS8618
}
