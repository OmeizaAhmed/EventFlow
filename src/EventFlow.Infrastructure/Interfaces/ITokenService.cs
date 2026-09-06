namespace EventFlow.Infrastructure.Interfaces;
using EventFlow.Domain.ValueObject;
public interface ITokenService
{
    string GenerateToken(UserInfo userInfo);
}