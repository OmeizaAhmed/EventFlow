namespace EventFlow.Domain.Entities;

public class DeliveryAttempt
{
    public Guid DeliveryAttemptId { get; private set; }
    public Guid DeliveryId { get; private set; }       // FK back to parent
    public Delivery Delivery { get; private set; } = null!;
    public int AttemptNumber { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Succeeded { get; private set; }
    public string? ResponseBody { get; private set; }
    public long DurationMilliseconds { get; private set; }

    private DeliveryAttempt() { }  // EF Core

    internal DeliveryAttempt(
        Guid deliveryId,
        int attemptNumber,
        int? httpStatusCode,
        string? errorMessage,
        bool succeeded,
        DateTime attemptedAt,
        string? responseBody,
        long durationMilliseconds)
    {
        DeliveryAttemptId = Guid.NewGuid();
        DeliveryId = deliveryId;
        AttemptNumber = attemptNumber;
        HttpStatusCode = httpStatusCode;
        ErrorMessage = errorMessage;
        AttemptedAt = attemptedAt;
        Succeeded = succeeded;
        ResponseBody = responseBody;
        DurationMilliseconds = durationMilliseconds;
    }

    
}
