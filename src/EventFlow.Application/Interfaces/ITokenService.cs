namespace EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;
public interface ITokenService
{
    string GenerateToken(UserInfo userInfo);
}