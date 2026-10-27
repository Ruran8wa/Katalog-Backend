using System.Text.Json;
using System.Diagnostics;
using Katalog_Backend.DTO;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Mappers;
using Katalog_Backend.Models;
using Katalog_Backend.Observability;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Katalog_Backend.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IVariantRepository _variantRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IVariantRepository variantRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _variantRepository = variantRepository;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync(string? userId = null)
    {
        var orders = await _orderRepository.GetAllAsync(userId);
        return orders.Select(o => o.ToDto());
    }

    public async Task<OrderResponseDto> GetOrderByIdAsync(int id, OrderRequester requester)
    {
        var order = await GetVisibleOrderAsync(id, requester);
        return order.ToDto();
    }

    // Another customer's order is reported as missing, so its existence is not revealed.
    private async Task<Order> GetVisibleOrderAsync(int id, OrderRequester requester)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null || (!requester.IsAdmin && order.UserId != requester.UserId))
        {
            throw new OrderNotFoundException(id);
        }

        return order;
    }

    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto, string? currentUserId = null)
    {
        var userId = currentUserId ?? dto.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required to place an order.");
        }

        if (dto.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        var variant = await _variantRepository.GetByIdAsync(dto.VariantId);
        if (variant == null)
        {
            throw new VariantNotFoundException(dto.VariantId);
        }

        if (variant.Quantity < dto.Quantity)
        {
            throw new InsufficientStockException(dto.VariantId, dto.Quantity, variant.Quantity);
        }

        var unitPrice = variant.PriceOverride ?? variant.Product?.BasePrice ?? 0;

        var order = dto.ToEntity(userId, unitPrice);
        order.Status = OrderStatus.Pending;

        var created = await _orderRepository.CreateWithOutboxAsync(order, savedOrder =>
        {
            var @event = new OrderPlacedEvent(
                savedOrder.Id,
                savedOrder.UserId,
                savedOrder.VariantId,
                savedOrder.Quantity,
                savedOrder.CreatedAt
            );

            return new OutboxMessage
            {
                EventType = nameof(OrderPlacedEvent),
                Payload = JsonSerializer.Serialize(@event),
                SessionId = savedOrder.Id.ToString(),
                TraceParent = Activity.Current?.Id
            };
        });

        Telemetry.OrdersPlaced.Add(1);
        return created.ToDto();
    }

    public async Task<OrderResponseDto> UpdateOrderQuantityAsync(int id, int newQuantity, OrderRequester requester)
    {
        if (newQuantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.");
        }

        var order = await GetVisibleOrderAsync(id, requester);

        var variant = await _variantRepository.GetByIdAsync(order.VariantId);
        if (variant == null)
        {
            throw new VariantNotFoundException(order.VariantId);
        }

        var diff = newQuantity - order.Quantity;
        if (diff > 0)
        {
            if (variant.Quantity < diff)
            {
                throw new InsufficientStockException(variant.Id, diff, variant.Quantity);
            }
            variant.Quantity -= diff;
            await _variantRepository.UpdateAsync(variant);
        }
        else if (diff < 0)
        {
            variant.Quantity += (-diff);
            await _variantRepository.UpdateAsync(variant);
        }

        order.Quantity = newQuantity;
        var updated = await _orderRepository.UpdateAsync(order);

        return updated.ToDto();
    }

    public async Task<OrderResponseDto> CancelOrderAsync(int id, OrderRequester requester)
    {
        var order = await GetVisibleOrderAsync(id, requester);
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
        {
            throw new OrderStatusConflictException(id, order.Status, OrderAction.Cancel);
        }

        // Only a confirmed order has reserved stock to release.
        var fromStatus = order.Status;
        var releasedStock = fromStatus == OrderStatus.Confirmed ? order.Quantity : 0;
        order.Status = OrderStatus.Cancelled;
        var cancelled = await _orderRepository.SaveWithStockChangeAsync(order, releasedStock);

        var cancelledBy = requester.IsAdmin ? "admin" : "customer";
        Telemetry.OrdersCancelled.Add(1, new TagList
        {
            { "cancelled_by", cancelledBy },
            { "from_status", fromStatus.ToDomainName() }
        });
        _logger.LogInformation("Order {OrderId} cancelled by {CancelledBy}", order.Id, cancelledBy);

        return cancelled.ToDto();
    }

    public async Task DeleteOrderAsync(int id, OrderRequester requester)
    {
        var order = await GetVisibleOrderAsync(id, requester);

        var variant = await _variantRepository.GetByIdAsync(order.VariantId);
        if (variant != null)
        {
            variant.Quantity += order.Quantity;
            await _variantRepository.UpdateAsync(variant);
        }

        await _orderRepository.DeleteAsync(id);
    }
}
