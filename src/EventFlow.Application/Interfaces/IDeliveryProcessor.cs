namespace EventFlow.Application.Interfaces;

public interface IDeliveryProcessor
{
    /// <summary>Processes one available delivery; returns false when no work is due.</summary>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
