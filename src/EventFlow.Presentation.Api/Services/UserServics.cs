using System.Security.Claims;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Exceptions;

namespace EventFlow.Presentation.Api.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid UserId
    {
        get
        {
            var claim = _accessor.HttpContext?
                .User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(claim, out var id) ? id : throw new ForbiddenException("User ID not found in token.");
        }
    }
}
