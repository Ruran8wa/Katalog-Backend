using Katalog_Backend.DTO;

namespace Katalog_Backend.Services.Interfaces;

public interface IAuthService
{
    public Task<AuthResponseDto> RegisterAsync(RegisterDto model);
    public Task<AuthResponseDto> LoginAsync(LoginDto model);
}