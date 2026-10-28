using Katalog_Backend.DTO;

namespace Katalog_Backend.Services.Interfaces;

public interface IOrderService
{
    Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync(string? userId = null);
    Task<OrderResponseDto> GetOrderByIdAsync(int id, OrderRequester requester);
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto, string? currentUserId = null);
    Task<OrderResponseDto> UpdateOrderQuantityAsync(int id, int newQuantity, OrderRequester requester);
    Task<OrderResponseDto> CancelOrderAsync(int id, OrderRequester requester);
    Task DeleteOrderAsync(int id, OrderRequester requester);
}
