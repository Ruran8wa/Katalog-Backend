using Katalog_Backend.Data;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class OutboxRepository(ApplicationDbContext context) : IOutboxRepository
{
    public async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        return await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.RetryCount++;
            message.Error = error;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
