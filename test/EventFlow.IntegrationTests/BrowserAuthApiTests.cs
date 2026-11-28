using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventFlow.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.IntegrationTests;

public sealed partial class ProjectApiTests
{
    [Fact]
    public async Task AuthMe_RequiresUserCredentialsAndReturnsCurrentProfile()
    {
        var user = await RegisterAndLoginAsync();
        using var anonymous = _factory.CreateApiClient();
        using var rejected = await anonymous.GetAsync("/api/auth/me");
        await AssertStatusAsync(HttpStatusCode.Unauthorized, rejected);

        using var response = await user.Client.GetAsync("/api/auth/me");
        await AssertStatusAsync(HttpStatusCode.OK, response);
        var profile = await ReadAsync<AuthUserResponse>(response);
        Assert.Equal(user.UserId, profile.UserId);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal("Test", profile.FirstName);
        Assert.Equal("User", profile.LastName);
        Assert.NotNull(profile.Roles);
    }

    [Fact]
    public async Task AuthLogout_RevokesTheRotatedRefreshSessionWhileKeepingOtherSessions()
    {
        var user = await RegisterAndLoginAsync();
        using var browser = _factory.CreateApiClient();
        using var login = await browser.PostAsJsonAsync("/api/auth/login",
            new LoginInput(user.Email, "IntegrationPass123!"));
        await AssertStatusAsync(HttpStatusCode.OK, login);
        var cookie = ReadRefreshCookie(login);
        var legacyToken = cookie["refreshToken=".Length..];
        await WithDatabaseAsync(db => db.RefreshTokens.Where(token => token.Token == legacyToken)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.SessionId, (Guid?)null)));

        using var independentLogin = await browser.PostAsJsonAsync("/api/auth/login",
            new LoginInput(user.Email, "IntegrationPass123!"));
        await AssertStatusAsync(HttpStatusCode.OK, independentLogin);
        var independent = ReadRefreshCookie(independentLogin);

        using var refreshed = await PostWithRefreshCookieAsync(browser, "refresh", cookie);
        await AssertStatusAsync(HttpStatusCode.OK, refreshed);
        var replacement = ReadRefreshCookie(refreshed);
        Assert.NotEqual(cookie, replacement);
        using var consumed = await PostWithRefreshCookieAsync(browser, "refresh", cookie);
        await AssertStatusAsync(HttpStatusCode.Unauthorized, consumed);

        var token = (await ReadAsync<LoginResponse>(refreshed)).Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var profile = await browser.SendAsync(request);
        await AssertStatusAsync(HttpStatusCode.OK, profile);

        using var logout = await PostWithRefreshCookieAsync(browser, "logout", cookie);
        await AssertStatusAsync(HttpStatusCode.OK, logout);
        Assert.Equal("refreshToken=", ReadRefreshCookie(logout));
        using var rejected = await PostWithRefreshCookieAsync(browser, "refresh", replacement);
        await AssertStatusAsync(HttpStatusCode.Unauthorized, rejected);
        using var unaffected = await PostWithRefreshCookieAsync(browser, "refresh", independent);
        await AssertStatusAsync(HttpStatusCode.OK, unaffected);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie")).Split(';', 2)[0];

    private static async Task<HttpResponseMessage> PostWithRefreshCookieAsync(HttpClient browser, string route, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{route}");
        request.Headers.Add("Cookie", cookie);
        return await browser.SendAsync(request);
    }
}
