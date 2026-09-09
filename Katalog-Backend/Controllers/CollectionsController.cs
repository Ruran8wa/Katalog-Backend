using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionsController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CollectionResponseDto>>> GetAll()
    {
        var collections = await _collectionService.GetAllCollectionsAsync();
        return Ok(collections);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CollectionResponseDto>> GetById(int id)
    {
        try
        {
            var collection = await _collectionService.GetCollectionByIdAsync(id);
            return Ok(collection);
        }
        catch (CollectionNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<CollectionResponseDto>> Create([FromBody] CreateCollectionDto createDto)
    {
        try
        {
            var collection = await _collectionService.CreateCollectionAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = collection.Id }, collection);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DuplicateCollectionNameException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CollectionResponseDto>> Update(int id, [FromBody] UpdateCollectionDto updateDto)
    {
        try
        {
            var collection = await _collectionService.UpdateCollectionAsync(id, updateDto);
            return Ok(collection);
        }
        catch (CollectionNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DuplicateCollectionNameException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _collectionService.DeleteCollectionAsync(id);
            return NoContent();
        }
        catch (CollectionNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/products/{productId}")]
    public async Task<IActionResult> AddProduct(int id, int productId)
    {
        try
        {
            await _collectionService.AddProductToCollectionAsync(id, productId);
            return NoContent();
        }
        catch (CollectionNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ProductAlreadyInCollectionException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id}/products/{productId}")]
    public async Task<IActionResult> RemoveProduct(int id, int productId)
    {
        try
        {
            await _collectionService.RemoveProductFromCollectionAsync(id, productId);
            return NoContent();
        }
        catch (CollectionNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ProductNotInCollectionException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
