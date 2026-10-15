using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.Interfaces;
namespace EventFlow.Infrastructure.Services;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

public sealed class SigningKeyService
    : ISigningKeyService
{
    private const string Purpose =
        "EventFlow.SigningKeys.v1";

    private readonly IDataProtector _protector;

    public SigningKeyService(
        IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _protector = provider.CreateProtector(Purpose);
    }

    public string GenerateSigningKey()
    {
        // Generate 256 bits of cryptographically secure randomness.
        byte[] key = RandomNumberGenerator.GetBytes(32);

        // Prefix helps identify the value as a webhook secret.
        return $"whsec_{Convert.ToBase64String(key)}";
    }

    public string Encrypt(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        return _protector.Protect(signingKey);
    }

    public string Decrypt(string encryptedSigningKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            encryptedSigningKey);

        return _protector.Unprotect(encryptedSigningKey);
    }
}

