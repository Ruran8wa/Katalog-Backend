using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

public class RegisterDto
{
    [Required]
    public string? FirstName { get; set; }
    [Required]
    public string? LastName { get; set; }
    
    [Required]
    [EmailAddress]
    public string? Email { get; set; }
    
    [Required]
    [MinLength(8)]
    public string? Password { get; set; }
}

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    
    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}

public record AuthResponseDto
(
     string Token,
     string Email, 
     string Role 
);