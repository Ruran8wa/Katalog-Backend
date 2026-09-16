using System.Text.Json;
using Katalog_Backend.DTO;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Mappers;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Katalog_Backend.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IVariantRepository _variantRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderService(
        IOrderRepository orderRepository,
        IVariantRepository variantRepository,
        UserManager<ApplicationUser> userManager)
    {
        _orderRepository = orderRepository;
        _variantRepository = variantRepository;
        _userManager = userManager;
    }

    public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync(string? userId = null)
    {
        var orders = await _orderRepository.GetAllAsync(userId);
        return orders.Select(o => o.ToDto());
    }

    public async Task<OrderResponseDto> GetOrderByIdAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            throw new OrderNotFoundException(id);
        }

        return order.ToDto();
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
                Payload = JsonSerializer.Serialize(@event)
            };
        });

        return created.ToDto();
    }

    public async Task<OrderResponseDto> UpdateOrderQuantityAsync(int id, int newQuantity)
    {
        if (newQuantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.");
        }

        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            throw new OrderNotFoundException(id);
        }

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

    public async Task DeleteOrderAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            throw new OrderNotFoundException(id);
        }

        var variant = await _variantRepository.GetByIdAsync(order.VariantId);
        if (variant != null)
        {
            variant.Quantity += order.Quantity;
            await _variantRepository.UpdateAsync(variant);
        }

        await _orderRepository.DeleteAsync(id);
    }
}
