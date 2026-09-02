using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VariantsController : ControllerBase
{
    private readonly IVariantService _variantService;

    public VariantsController(IVariantService variantService)
    {
        _variantService = variantService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VariantResponseDto>>> GetAll([FromQuery] int? productId)
    {
        try
        {
            var variants = await _variantService.GetAllVariantsAsync(productId);
            return Ok(variants);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VariantResponseDto>> GetById(int id)
    {
        try
        {
            var variant = await _variantService.GetVariantByIdAsync(id);
            return Ok(variant);
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<VariantResponseDto>> Create([FromBody] CreateVariantDto createDto)
    {
        try
        {
            var variant = await _variantService.CreateVariantAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = variant.Id }, variant);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (DuplicateSkuException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VariantResponseDto>> Update(int id, [FromBody] UpdateVariantDto updateDto)
    {
        try
        {
            var variant = await _variantService.UpdateVariantAsync(id, updateDto);
            return Ok(variant);
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (DuplicateSkuException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _variantService.DeleteVariantAsync(id);
            return NoContent();
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/images")]
    public async Task<ActionResult<VariantImageResponseDto>> UploadImage(
        int id,
        IFormFile file,
        [FromQuery] bool isPrimary = false,
        [FromQuery] int displayOrder = 0)
    {
        try
        {
            var image = await _variantService.UploadVariantImageAsync(id, file, isPrimary, displayOrder);
            return Ok(image);
        }
        catch (VariantNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpDelete("{id}/images/{imageId}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        try
        {
            await _variantService.DeleteVariantImageAsync(id, imageId);
            return NoContent();
        }
        catch (VariantImageNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/images/{imageId}/primary")]
    public async Task<IActionResult> SetPrimaryImage(int id, int imageId)
    {
        try
        {
            await _variantService.SetPrimaryVariantImageAsync(id, imageId);
            return NoContent();
        }
        catch (VariantImageNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
