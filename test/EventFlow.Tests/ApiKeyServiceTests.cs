using EventFlow.Infrastructure.Services;

namespace EventFlow.Tests;

public class ApiKeyServiceTests
{
    private readonly ApiKeyService _service = new();

    [Theory]
    [InlineData(true, "live")]
    [InlineData(false, "test")]
    public void Generate_ReturnsUniqueVerifiableKey(bool isLive, string environment)
    {
        var key = _service.Generate(isLive);
        Assert.Matches($"^ef_{environment}_[a-f0-9]{{8}}_[A-Za-z0-9_-]{{43}}$", key.PlainTextKey);
        Assert.Equal(key.KeyPrefix, _service.ExtractPrefix(key.PlainTextKey));
        Assert.Matches("^[a-f0-9]{64}$", key.HashedKey);
        Assert.True(_service.Verify(key.PlainTextKey, key.HashedKey));
        Assert.NotEqual(key.PlainTextKey, _service.Generate(isLive).PlainTextKey);
    }

    [Fact]
    public void Hash_MatchesKnownSha256Vector()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", _service.Hash("abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Hash_BlankInput_Throws(string? input) =>
        Assert.ThrowsAny<ArgumentException>(() => _service.Hash(input!));

    [Theory]
    [InlineData(null, "abcd")]
    [InlineData("", "abcd")]
    [InlineData("secret", null)]
    [InlineData("secret", " ")]
    [InlineData("secret", "not-hex")]
    [InlineData("secret", "abc")]
    [InlineData("secret", "ab")]
    public void Verify_InvalidInput_ReturnsFalse(string? key, string? hash) =>
        Assert.False(_service.Verify(key!, hash!));

    [Fact]
    public void Verify_ChangedSecret_ReturnsFalse() =>
        Assert.False(_service.Verify("different", _service.Hash("original")));

    [Theory]
    [InlineData("ef_live_1234abcd_secret_with_underscores", "ef_live_1234abcd")]
    [InlineData("ef_test_1234abcd_secret", "ef_test_1234abcd")]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("ef_live_1234abcd", null)]
    [InlineData("other_live_1234abcd_secret", null)]
    [InlineData("ef_unknown_1234abcd_secret", null)]
    public void ExtractPrefix_ParsesSupportedKeys(string? key, string? expected) =>
        Assert.Equal(expected, _service.ExtractPrefix(key!));
}
