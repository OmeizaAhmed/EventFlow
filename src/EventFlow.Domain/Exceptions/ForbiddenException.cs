using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Domain.Exceptions
{
    public sealed class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message, System.Net.HttpStatusCode.Forbidden)
        {
        }
    }
}
