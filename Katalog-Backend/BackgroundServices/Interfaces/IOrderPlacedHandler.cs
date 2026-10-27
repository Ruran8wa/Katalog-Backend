using Katalog_Backend.Events;

namespace Katalog_Backend.BackgroundServices.Interfaces;

public enum OrderProcessingOutcome
{
    Confirmed,
    Rejected,
    Skipped
}

// Reason is null when the order was confirmed.
public sealed record OrderProcessingResult(OrderProcessingOutcome Outcome, string? Reason = null);

public interface IOrderPlacedHandler
{
    Task<OrderProcessingResult> HandleAsync(OrderPlacedEvent orderPlaced, CancellationToken cancellationToken = default);
}
