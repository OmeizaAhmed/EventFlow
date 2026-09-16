namespace EventFlow.Domain.Exceptions;
using System;
using System.Net;
public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message, HttpStatusCode.UnprocessableEntity)
    {
    }
}