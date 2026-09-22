namespace EventFlow.Application.Interfaces;
using System.Threading.Tasks;
using EventFlow.Application.DTOs;
public interface IAuthService
{
    Task RegisterUserAsync(RegisterInput registerInput);
    Task<string> LoginUserAsync(string email, string password);
}