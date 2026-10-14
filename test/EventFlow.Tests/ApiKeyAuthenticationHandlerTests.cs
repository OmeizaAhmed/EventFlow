using System.Text.Encodings.Web;
using EventFlow.Infrastructure.Authentication;
using EventFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventFlow.Tests;

public partial class ProjectServiceTests
{
    // Fixed secret includes underscores, which are valid Base64Url characters.
    private const string AuthenticationKey = "ef_test_1234abcd_secret_with_underscores";

    [Theory]
    [InlineData("Bearer")]
    [InlineData("bearer")]
    [InlineData("X-API-Key")]
    public async Task AuthenticationHandler_ValidKey_ReturnsProjectKeyAndEnvironmentClaims(string header)
    {
        var key = _creation.Project.GenerateApiKey(new ApiKeyService().Hash(AuthenticationKey), "ef_test_1234abcd", false);
        _repository.ApiKeys.Add(key);
        var context = new DefaultHttpContext();
        context.Request.RouteValues["projectId"] = ProjectId.ToString();
        if (header == "X-API-Key") context.Request.Headers[header] = AuthenticationKey;
        else context.Request.Headers.Authorization = $"{header} {AuthenticationKey}";

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.True(result.Principal!.Identity!.IsAuthenticated);
        Assert.Equal(ApiKeyDefaults.Scheme, result.Ticket!.AuthenticationScheme);
        Assert.Equal(key.ApiKeyId.ToString(), result.Principal.FindFirst(ApiKeyDefaults.ApiKeyIdClaim)?.Value);
        Assert.Equal(ProjectId.ToString(), result.Principal.FindFirst(ApiKeyDefaults.ProjectIdClaim)?.Value);
        Assert.Equal("test", result.Principal.FindFirst(ApiKeyDefaults.EnvironmentClaim)?.Value);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-secret")]
    [InlineData("revoked")]
    [InlineData("other-project")]
    [InlineData("malformed")]
    public async Task AuthenticationHandler_InvalidKey_ReturnsSameFailureWithoutPrincipal(string scenario)
    {
        var key = _creation.Project.GenerateApiKey(new ApiKeyService().Hash(AuthenticationKey), "ef_test_1234abcd", false);
        if (scenario != "unknown") _repository.ApiKeys.Add(key);
        if (scenario == "revoked") _creation.Project.RevokeApiKey(key.ApiKeyId);
        var context = new DefaultHttpContext();
        context.Request.RouteValues["projectId"] = scenario == "other-project" ? Guid.NewGuid().ToString() : ProjectId.ToString();
        context.Request.Headers["X-API-Key"] = scenario switch
        {
            "wrong-secret" => "ef_test_1234abcd_wrong",
            "malformed" => "invalid",
            _ => AuthenticationKey
        };

        var result = await AuthenticateAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API key.", result.Failure?.Message);
        Assert.Null(result.Principal);
    }

    [Theory]
    [InlineData("missing-key")]
    [InlineData("missing-project")]
    [InlineData("invalid-project")]
    public async Task AuthenticationHandler_MissingRequestInput_ReturnsNoResult(string scenario)
    {
        var context = new DefaultHttpContext();
        if (scenario != "missing-project")
            context.Request.RouteValues["projectId"] = scenario == "invalid-project" ? "invalid" : ProjectId.ToString();
        if (scenario != "missing-key") context.Request.Headers["X-API-Key"] = AuthenticationKey;

        var result = await AuthenticateAsync(context);

        Assert.True(result.None);
        Assert.Null(result.Principal);
    }

    private async Task<AuthenticateResult> AuthenticateAsync(DefaultHttpContext context)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var handler = new ApiKeyAuthenticationHandler(
            services.GetRequiredService<IOptionsMonitor<AuthenticationSchemeOptions>>(),
            NullLoggerFactory.Instance, UrlEncoder.Default, new ApiKeyService(), _repository);
        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyDefaults.Scheme, null, typeof(ApiKeyAuthenticationHandler)), context);
        return await handler.AuthenticateAsync();
    }
}
