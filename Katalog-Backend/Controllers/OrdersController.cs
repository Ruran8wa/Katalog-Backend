using System.Security.Claims;
using Katalog_Backend.DTO;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Controllers;

// Domain exceptions are mapped to status codes by GlobalExceptionHandler.
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private bool IsAdmin => User.IsInRole("Admin");

    private OrderRequester Requester => new(CurrentUserId, IsAdmin);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetAll([FromQuery] string? userId)
    {
        var effectiveUserId = IsAdmin ? userId : CurrentUserId;

        var orders = await _orderService.GetAllOrdersAsync(effectiveUserId);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id, Requester);
        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create([FromBody] CreateOrderDto dto)
    {
        var effectiveUserId = IsAdmin && !string.IsNullOrWhiteSpace(dto.UserId) ? dto.UserId : CurrentUserId;

        var order = await _orderService.CreateOrderAsync(dto, effectiveUserId);
        return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPut("{id:int}/quantity")]
    public async Task<ActionResult<OrderResponseDto>> UpdateQuantity(int id, [FromBody] UpdateOrderQuantityDto dto)
    {
        var updated = await _orderService.UpdateOrderQuantityAsync(id, dto.Quantity, Requester);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _orderService.DeleteOrderAsync(id, Requester);
        return NoContent();
    }
}
