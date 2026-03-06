namespace Insolvio.Core.Exceptions;

/// <summary>Thrown when the current user does not have permission to access a resource (maps to HTTP 403).</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to access this resource.") { }
    public ForbiddenException(string message) : base(message) { }
}
