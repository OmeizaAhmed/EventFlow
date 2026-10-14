using System.Security.Cryptography;
using System.Text;
using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;

namespace EventFlow.Infrastructure.Services;

public sealed class ApiKeyService : IApiKeyService
{
    private const string LivePrefix = "ef_live_";
    private const string TestPrefix = "ef_test_";

    private const int PublicIdBytes = 4;   // -> 8 hex chars, used for DB lookup + dashboard display
    private const int SecretBytes = 32;    // 256 bits of entropy

    public GeneratedApiKey Generate(bool isLive)
    {
        var environment = isLive ? LivePrefix : TestPrefix;

        var publicId = Convert.ToHexString(RandomNumberGenerator.GetBytes(PublicIdBytes)).ToLowerInvariant();
        var secret = ToBase64Url(RandomNumberGenerator.GetBytes(SecretBytes));

        var keyPrefix = $"{environment}{publicId}";        // ef_live_8a2f1c3d
        var plainTextKey = $"{keyPrefix}_{secret}";        // ef_live_8a2f1c3d_<43 chars>

        return new GeneratedApiKey(plainTextKey, keyPrefix, Hash(plainTextKey));
    }

 
    public string Hash(string plainTextKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextKey);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hashes the key the client presented and compares it to the stored hash
    /// in constant time to avoid timing attacks.
    /// </summary>
    public bool Verify(string presentedKey, string storedHashedKey)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) || string.IsNullOrWhiteSpace(storedHashedKey))
            return false;

        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));

        byte[] storedHash;
        try
        {
            storedHash = Convert.FromHexString(storedHashedKey);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(presentedHash, storedHash);
    }

    /// <summary>
    /// Pulls "ef_live_8a2f1c3d" out of "ef_live_8a2f1c3d_secret" so you can look up
    /// the candidate row by prefix before verifying the hash. Returns null if malformed.
    /// </summary>
    public string? ExtractPrefix(string presentedKey)
    {
        if (string.IsNullOrWhiteSpace(presentedKey)) return null;

        var parts = presentedKey.Split('_');
        // ["ef", "live", "8a2f1c3d", "<secret>"]
        if (parts.Length != 4 || parts[0] != "ef" || (parts[1] != "live" && parts[1] != "test"))
            return null;

        return $"{parts[0]}_{parts[1]}_{parts[2]}";
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

/* ---------------------------------------------------------------
 * Registration:
 *   builder.Services.AddSingleton<IApiKeyService, ApiKeyService>();
 *
 * Authentication flow (e.g. in middleware / auth handler):
 *   var prefix = _apiKeyService.ExtractPrefix(presentedKey);
 *   if (prefix is null) return Unauthorized();
 *
 *   var apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyPrefix == prefix);
 *   if (apiKey is null || apiKey.IsRevoked
 *       || !_apiKeyService.Verify(presentedKey, apiKey.HashedKey))
 *       return Unauthorized();
 *
 * EF Core: add a unique index on ApiKey.KeyPrefix. With 4 random bytes a collision
 * is rare but possible, so if the insert hits the index, call Generate again.
 * --------------------------------------------------------------- */
