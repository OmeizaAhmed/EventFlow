using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventFlow.Application.DTOs;
using EventFlow.Domain.Enums;
using EventFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests
{
    [Theory]
    [InlineData(true, "Bearer")]
    [InlineData(false, "Bearer")]
    [InlineData(true, "X-API-Key")]
    [InlineData(false, "X-API-Key")]
    public async Task CreateApiKey_PersistsOnlyHash_AuthenticatesAndDoesNotExposeSecretAgain(bool isLive, string header)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        using var creation = await owner.Client.PostAsJsonAsync($"/api/projects/{project.ProjectId}/apikeys", new { IsLive = isLive });
        await AssertStatusAsync(HttpStatusCode.Created, creation);
        var key = await ReadAsync<CreatedApiKeyResponse>(creation);
        Assert.Equal(project.ProjectId, key.ProjectId);
        Assert.Equal(isLive, key.IsLive);
        Assert.Equal($"/api/projects/{project.ProjectId}/apikeys/{key.ApiKeyId}",
            creation.Headers.Location?.AbsolutePath, ignoreCase: true);

        await WithDatabaseAsync(async db =>
        {
            var stored = Assert.Single(await db.ApiKeys.AsNoTracking().ToListAsync());
            Assert.Equal(key.ApiKeyId, stored.ApiKeyId);
            Assert.Equal(project.ProjectId, stored.ProjectId);
            Assert.Equal(key.KeyPrefix, stored.KeyPrefix);
            Assert.Equal(key.CreatedAt, stored.CreatedAt);
            Assert.Equal(isLive, stored.IsLive);
            Assert.Null(stored.RevokedAt);
            Assert.NotEqual(key.plainTextKey, stored.HashedKey);
            Assert.True(new ApiKeyService().Verify(key.plainTextKey, stored.HashedKey));
        });

        foreach (var url in new[] { creation.Headers.Location!.ToString(), $"/api/projects/{project.ProjectId}/apikeys" })
        {
            using var read = await owner.Client.GetAsync(url);
            await AssertStatusAsync(HttpStatusCode.OK, read);
            var json = await read.Content.ReadAsStringAsync();
            Assert.DoesNotContain(key.plainTextKey, json);
            Assert.DoesNotContain("plainTextKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hashedKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(key.KeyPrefix, json);
        }

        using var client = _factory.CreateApiClient();
        SetApiKey(client, header, key.plainTextKey);
        using var response = await client.GetAsync(ApiKeyTestUrl(project.ProjectId));
        await AssertStatusAsync(HttpStatusCode.OK, response);
        var claims = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(project.ProjectId.ToString(), claims.GetProperty("projectId").GetString());
        Assert.Equal(key.ApiKeyId.ToString(), claims.GetProperty("apiKeyId").GetString());
        Assert.Equal(isLive ? "live" : "test", claims.GetProperty("environment").GetString());
    }

    [Theory]
    [InlineData(ProjectMembershipRole.Admin, HttpStatusCode.Created)]
    [InlineData(ProjectMembershipRole.Developer, HttpStatusCode.Forbidden)]
    [InlineData(ProjectMembershipRole.ReadOnly, HttpStatusCode.Forbidden)]
    public async Task CreateApiKey_EnforcesMembershipRole(ProjectMembershipRole role, HttpStatusCode expected)
    {
        var owner = await RegisterAndLoginAsync();
        var member = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        await AddMembershipAsync(project.ProjectId, owner.UserId, member.UserId, role);
        using var response = await member.Client.PostAsJsonAsync($"/api/projects/{project.ProjectId}/apikeys", new { IsLive = false });
        await AssertStatusAsync(expected, response);
        await WithDatabaseAsync(async db => Assert.Equal(expected == HttpStatusCode.Created ? 1 : 0, await db.ApiKeys.CountAsync()));
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("non-member", HttpStatusCode.Forbidden)]
    [InlineData("inactive", HttpStatusCode.UnprocessableEntity)]
    public async Task CreateApiKey_RejectedRequest_DoesNotPersist(string scenario, HttpStatusCode expected)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        using var anonymous = _factory.CreateApiClient();
        var client = owner.Client;
        if (scenario == "anonymous") client = anonymous;
        if (scenario == "non-member") client = (await RegisterAndLoginAsync()).Client;
        if (scenario == "inactive") await DeactivateAsync(owner.Client, project.ProjectId);

        using var response = await client.PostAsJsonAsync($"/api/projects/{project.ProjectId}/apikeys", new { IsLive = true });
        await AssertStatusAsync(expected, response);
        await WithDatabaseAsync(async db => Assert.Empty(await db.ApiKeys.ToListAsync()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("wrong-secret")]
    [InlineData("other-project")]
    [InlineData("revoked")]
    [InlineData("jwt-only")]
    [InlineData("invalid-bearer-valid-header")]
    public async Task AuthenticateApiKey_InvalidCredentials_ReturnsUnauthorized(string scenario)
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var key = await CreateKeyAsync(owner.Client, project.ProjectId);
        using var client = _factory.CreateApiClient();
        var projectId = project.ProjectId;
        var secret = key.plainTextKey;
        switch (scenario)
        {
            case "missing": secret = null; break;
            case "malformed": secret = "not-an-api-key"; break;
            case "unknown": secret = new ApiKeyService().Generate(false).PlainTextKey; break;
            case "wrong-secret": secret = key.KeyPrefix + "_incorrect-secret"; break;
            case "other-project": projectId = (await CreateProjectAsync(owner.Client, "Other project")).ProjectId; break;
            case "revoked":
                using (var revoke = await owner.Client.PostAsync($"/api/projects/{projectId}/apikeys/{key.ApiKeyId}/revoke", null))
                    await AssertStatusAsync(HttpStatusCode.NoContent, revoke);
                break;
            case "jwt-only":
                secret = null;
                client.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
                break;
            case "invalid-bearer-valid-header":
                client.DefaultRequestHeaders.Add("X-API-Key", secret);
                secret = "invalid";
                break;
        }
        if (secret is not null) SetApiKey(client, "Bearer", secret);

        using var response = await client.GetAsync(ApiKeyTestUrl(projectId));
        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task AuthenticateApiKey_ScheduledRevocation_RemainsValidDuringGracePeriod()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var key = await CreateKeyAsync(owner.Client, project.ProjectId);
        await WithDatabaseAsync(async db =>
        {
            var stored = await db.Projects.Include(p => p.ApiKeys).SingleAsync(p => p.ProjectId == project.ProjectId);
            stored.RevokeApiKey(key.ApiKeyId, DateTime.UtcNow.AddHours(1));
            await db.SaveChangesAsync();
        });
        using var client = _factory.CreateApiClient();
        SetApiKey(client, "Bearer", key.plainTextKey);
        using var response = await client.GetAsync(ApiKeyTestUrl(project.ProjectId));
        await AssertStatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task ApiKey_CannotCreateMoreApiKeysWithoutUserCredentials()
    {
        var owner = await RegisterAndLoginAsync();
        var project = await CreateProjectAsync(owner.Client);
        var key = await CreateKeyAsync(owner.Client, project.ProjectId);
        using var client = _factory.CreateApiClient();
        SetApiKey(client, "Bearer", key.plainTextKey);
        using var response = await client.PostAsJsonAsync($"/api/projects/{project.ProjectId}/apikeys", new { IsLive = true });
        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        await WithDatabaseAsync(async db => Assert.Single(await db.ApiKeys.ToListAsync()));
    }

    private static string ApiKeyTestUrl(Guid projectId) => $"/api/projects/{projectId}/events/apikey-test";

    private static void SetApiKey(HttpClient client, string header, string key)
    {
        if (header == "Bearer") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        else client.DefaultRequestHeaders.Add(header, key);
    }

    private static async Task<CreatedApiKeyResponse> CreateKeyAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/apikeys", new { IsLive = false });
        await AssertStatusAsync(HttpStatusCode.Created, response);
        return await ReadAsync<CreatedApiKeyResponse>(response);
    }
}
