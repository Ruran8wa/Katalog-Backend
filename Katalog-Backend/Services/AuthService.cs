using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Katalog_Backend.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService) : IAuthService
{
    private const string DefaultRole = "Customer";

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };
            
        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new RegistrationException(errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, DefaultRole);
        if (!roleResult.Succeeded)
        {
            var errors = roleResult.Errors.Select(e => e.Description).ToList();
            throw new RegistrationException("Failed to assign default role", errors);
        }

        var roles = new List<string> { DefaultRole };
        var token = tokenService.CreateToken(user.Id, user.Email, roles);

        return new AuthResponseDto
        (
            token,
            user.Email,
            roles
        );
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new AccountLockedException();
        }

        if (!result.Succeeded)
        {
            throw new InvalidCredentialsException();
        }

        var roles = await userManager.GetRolesAsync(user);
        
        var token = tokenService.CreateToken(user.Id, user.Email!, roles);

        return new AuthResponseDto(
            token,
            user.Email!,
            roles
        );
    }
}