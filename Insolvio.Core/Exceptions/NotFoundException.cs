namespace Insolvio.Core.Exceptions;

/// <summary>Thrown when a requested resource does not exist (maps to HTTP 404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entityType, Guid id) : base($"{entityType} '{id}' was not found.") { }
}
