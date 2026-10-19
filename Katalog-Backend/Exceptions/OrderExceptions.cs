namespace Katalog_Backend.Exceptions;

public class OrderNotFoundException(int id)
    : AppException($"Order with ID {id} was not found.");

public class InsufficientStockException(int variantId, int requested, int available)
    : AppException($"Insufficient stock for variant with ID {variantId}. Requested: {requested}, Available: {available}.");

public class InvalidOrderOperationException(string message)
    : AppException(message);
