using System.Security.Claims;
using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetAll([FromQuery] string? userId)
    {
        var orders = await _orderService.GetAllOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            return Ok(order);
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create([FromBody] CreateOrderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _orderService.CreateOrderAsync(dto, currentUserId);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/quantity")]
    public async Task<ActionResult<OrderResponseDto>> UpdateQuantity(int id, [FromBody] UpdateOrderQuantityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _orderService.UpdateOrderQuantityAsync(id, dto.Quantity);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _orderService.DeleteOrderAsync(id);
            return NoContent();
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
