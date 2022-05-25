using System.Runtime.Serialization;

namespace Microsoft.DotnetOrg.DevOps;

[Serializable]
public class DevOpsUnauthorizedException : Exception
{
    public DevOpsUnauthorizedException()
    {
    }

    public DevOpsUnauthorizedException(string? message)
        : base(message)
    {
    }

    public DevOpsUnauthorizedException(string? message, Exception? inner)
        : base(message, inner)
    {
    }

    protected DevOpsUnauthorizedException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}