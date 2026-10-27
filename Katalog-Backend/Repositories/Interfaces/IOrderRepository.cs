using Katalog_Backend.Models;

namespace Katalog_Backend.Repositories.Interfaces;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetAllAsync(string? userId = null);
    Task<Order?> GetByIdAsync(int id);
    Task<Order> CreateAsync(Order order);
    Task<Order> CreateWithOutboxAsync(Order order, Func<Order, OutboxMessage> outboxMessageFactory);
    // Saves changes to a tracked order and adjusts its variant's stock by stockDelta in one transaction.
    // Throws InsufficientStockException if a negative delta exceeds the stock, and
    // OrderConcurrencyConflictException if the order changed since it was read.
    Task<Order> SaveWithStockChangeAsync(Order order, int stockDelta);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
