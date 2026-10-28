using Katalog_Backend.Enums;

namespace Katalog_Backend.Exceptions;

public class OrderNotFoundException(int id)
    : AppException($"Order with ID {id} was not found.");

public class InsufficientStockException(int variantId, int requested, int available)
    : AppException($"Insufficient stock for variant with ID {variantId}. Requested: {requested}, Available: {available}.");

public class InvalidOrderOperationException(string message)
    : AppException(message);

public enum OrderAction
{
    Cancel,
    ChangeQuantity,
    Delete
}

public class OrderStatusConflictException(int id, OrderStatus status, OrderAction action)
    : AppException($"Order with ID {id} is {status.ToDomainName()} and {Describe(action)}.")
{
    private static string Describe(OrderAction action) => action switch
    {
        OrderAction.Cancel => "cannot be cancelled",
        OrderAction.ChangeQuantity => "cannot be changed",
        OrderAction.Delete => "cannot be deleted; cancel it first",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };
}

public class OrderActionForbiddenException(string message)
    : AppException(message);

public class OrderConcurrencyConflictException(int id)
    : AppException($"Order with ID {id} was changed by another request. Reload it and try again.");
