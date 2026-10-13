using EventFlow.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.Interfaces;

public interface IApiKeyService
{
    GeneratedApiKey Generate(bool isLive);
    string Hash(string plainTextKey);
    bool Verify(string presentedKey, string storedHashedKey);
    string? ExtractPrefix(string presentedKey);
}
