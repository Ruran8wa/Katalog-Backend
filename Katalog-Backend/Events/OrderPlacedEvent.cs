namespace Katalog_Backend.Events;

public record OrderPlacedEvent(
    int OrderId,
    string UserId,
    int VariantId,
    int Quantity,
    DateTime CreatedAt
);
