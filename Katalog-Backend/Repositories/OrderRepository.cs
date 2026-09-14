using Katalog_Backend.Data;
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

    public async Task<Order> UpdateAsync(Order order)
    {
        context.Orders.Update(order);
        await context.SaveChangesAsync();
        return order;
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
