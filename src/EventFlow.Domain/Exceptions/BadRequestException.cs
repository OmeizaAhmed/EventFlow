namespace EventFlow.Domain.Exceptions;
using System;
using System.Net;

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, HttpStatusCode.BadRequest)
    {
    }
}