namespace EventFlow.Application.Interfaces;

public interface ICurrentUser
{
    Guid UserId { get; }
}