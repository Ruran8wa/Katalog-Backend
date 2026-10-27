using Katalog_Backend.Data;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class OrderRepository(ApplicationDbContext context) : IOrderRepository
{
    public async Task<IEnumerable<Order>> GetAllAsync(string? userId = null)
    {
        var query = context.Orders
            .Include(o => o.User)
            .Include(o => o.Variant)
                .ThenInclude(v => v.Product)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(o => o.UserId == userId);
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await context.Orders
            .Include(o => o.User)
            .Include(o => o.Variant)
                .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order> CreateAsync(Order order)
    {
        await context.Orders.AddAsync(order);
        await context.SaveChangesAsync();

        await context.Entry(order).Reference(o => o.User).LoadAsync();
        await context.Entry(order).Reference(o => o.Variant).LoadAsync();
        if (order.Variant != null)
        {
            await context.Entry(order.Variant).Reference(v => v.Product).LoadAsync();
        }
        return order;
    }

    public async Task<Order> CreateWithOutboxAsync(Order order, Func<Order, OutboxMessage> outboxMessageFactory)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                await context.Orders.AddAsync(order);
                await context.SaveChangesAsync();

                var outboxMessage = outboxMessageFactory(order);
                await context.OutboxMessages.AddAsync(outboxMessage);
                await context.SaveChangesAsync();

                await transaction.CommitAsync();

                await context.Entry(order).Reference(o => o.User).LoadAsync();
                await context.Entry(order).Reference(o => o.Variant).LoadAsync();
                if (order.Variant != null)
                {
                    await context.Entry(order.Variant).Reference(v => v.Product).LoadAsync();
                }

                return order;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<Order> SaveWithStockChangeAsync(Order order, int stockDelta)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            if (stockDelta > 0)
            {
                await context.Variants.ReleaseStockAsync(order.VariantId, stockDelta);
            }
            else if (stockDelta < 0 && !await context.Variants.TryTakeStockAsync(order.VariantId, -stockDelta))
            {
                var available = await context.Variants
                    .Where(v => v.Id == order.VariantId)
                    .Select(v => v.Quantity)
                    .FirstOrDefaultAsync();
                throw new InsufficientStockException(order.VariantId, -stockDelta, available);
            }

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new OrderConcurrencyConflictException(order.Id);
            }

            await transaction.CommitAsync();
            return order;
        });
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var order = await context.Orders.FindAsync(id);
        if (order == null)
        {
            return false;
        }

        context.Orders.Remove(order);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await context.Orders.AnyAsync(o => o.Id == id);
    }
}
