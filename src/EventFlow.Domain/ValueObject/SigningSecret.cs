namespace EventFlow.Domain.ValueObject;
using System;
public class SigningSecret
{
    public string Value { get; }

    public SigningSecret(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}