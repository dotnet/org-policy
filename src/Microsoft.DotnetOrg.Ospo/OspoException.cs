using System.Net;
using System.Runtime.Serialization;

namespace Microsoft.DotnetOrg.Ospo;

public class OspoException : Exception
{
    public OspoException()
    {
    }

    public OspoException(string? message)
        : base(message)
    {
    }

    public OspoException(string? message, Exception? inner)
        : base(message, inner)
    {
    }

    public OspoException(string? message, HttpStatusCode code)
        : this(message)
    {
        Code = code;
    }

    public HttpStatusCode Code { get; }
}