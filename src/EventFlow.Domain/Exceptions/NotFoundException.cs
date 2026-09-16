namespace EventFlow.Domain.Exceptions;
using System;
using System.Net;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string resourceName, object key) : base($"'{resourceName}' with identifier '{key}' was not found.", HttpStatusCode.NotFound)
    {
    }
}