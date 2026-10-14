using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace EventFlow.Infrastructure.Authentication
{
    public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly IProjectRepository _repository;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiKeyService apiKeyService,
            IProjectRepository repository)
            : base(options, logger, encoder)
        {
            _apiKeyService = apiKeyService;
            _repository = repository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var presentedKey = ReadKeyFromRequest();

            // No key supplied: let the request continue unauthenticated.
            // [Authorize] will turn this into a 401 via HandleChallengeAsync.
            if (presentedKey is null)
                return AuthenticateResult.NoResult();

            var prefix = _apiKeyService.ExtractPrefix(presentedKey.ApiKey);
            if (prefix is null)
                return AuthenticateResult.Fail("Invalid API key.");

            var apiKey = await _repository.GetApiKeyByPrefixAsync(presentedKey.ProjectId, prefix);

            // Same message for unknown / revoked / wrong secret so callers can't
            // tell which keys exist.
            if (apiKey is null
                || apiKey.IsRevoked
                || !_apiKeyService.Verify(presentedKey.ApiKey, apiKey.HashedKey))
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }

            var claims = new[]
            {
            new Claim(ApiKeyDefaults.ApiKeyIdClaim, apiKey.ApiKeyId.ToString()),
            new Claim(ApiKeyDefaults.ProjectIdClaim, apiKey.ProjectId.ToString()),
            new Claim(ApiKeyDefaults.EnvironmentClaim, apiKey.IsLive ? "live" : "test"),
        };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.WWWAuthenticate = "Bearer";
            return Task.CompletedTask;
        }

        private ApiAuthInput? ReadKeyFromRequest()
        {
             var projectId = Guid.TryParse(Request.RouteValues["projectId"]?.ToString(), out var parsedProjectId) ? parsedProjectId : (Guid?)null;
             if (projectId == null)
            {
                return null;
            }

            // Preferred: Authorization: Bearer ef_live_...
            if (Request.Headers.TryGetValue("Authorization", out var auth))
            {
                var value = auth.ToString();
                const string bearer = "Bearer ";
                if (value.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
                    return new ApiAuthInput(projectId.Value, value[bearer.Length..].Trim());
            }

            // Fallback: X-API-Key: ef_live_...
            if (Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var header)
                && !string.IsNullOrWhiteSpace(header))
                return new ApiAuthInput(projectId.Value, header.ToString().Trim());

            return null;
        }
    }


    public static class ApiKeyDefaults
    {
        public const string Scheme = "ApiKey";
        public const string HeaderName = "X-API-Key";

        // Policy names
        public const string AnyKeyPolicy = "ApiKey";
        public const string LiveKeyPolicy = "ApiKeyLive";

        // Claim types
        public const string ProjectIdClaim = "project_id";
        public const string ApiKeyIdClaim = "api_key_id";
        public const string EnvironmentClaim = "key_environment"; // "live" | "test"
    }
    public record ApiAuthInput(Guid ProjectId, string ApiKey);
}
