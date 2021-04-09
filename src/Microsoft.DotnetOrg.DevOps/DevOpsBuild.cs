using System;

namespace Microsoft.DotnetOrg.DevOps
{
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
}
