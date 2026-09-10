namespace EventFlow.Domain.Entities;

public class DeliveryAttempt
{
    public Guid DeliveryAttemptId { get; private set; }
    public Guid DeliveryId { get; private set; }       // FK back to parent
    public int AttemptNumber { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Succeeded { get; private set; }

    private DeliveryAttempt() { }  // EF Core

    internal DeliveryAttempt(int attemptNumber, int? httpStatusCode, string? errorMessage, DateTime attemptedAt)
    {
        AttemptNumber = attemptNumber;
        HttpStatusCode = httpStatusCode;
        ErrorMessage = errorMessage;
        AttemptedAt = attemptedAt;
        Succeeded = httpStatusCode is >= 200 and < 300;
    }
}