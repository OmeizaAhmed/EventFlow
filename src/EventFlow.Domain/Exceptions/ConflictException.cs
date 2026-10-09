namespace EventFlow.Domain.Exceptions;
using System.Net;

public sealed class ConflictException : AppException
{
    public ConflictException(string message, HttpStatusCode statusCode = HttpStatusCode.Conflict) : base(message, statusCode)
    {
    }
}