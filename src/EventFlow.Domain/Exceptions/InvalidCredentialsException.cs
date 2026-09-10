namespace EventFlow.Domain.Exceptions;
using System;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message) : base(message)
    {
    }
}