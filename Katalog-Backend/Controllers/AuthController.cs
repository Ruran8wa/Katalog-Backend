using Katalog_Backend.DTO;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace Katalog_Backend.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var register = await authService.RegisterAsync(dto);
        return Ok(register);
    }

    [HttpPost]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var login = await authService.LoginAsync(dto);
        return Ok(login);
    }
}