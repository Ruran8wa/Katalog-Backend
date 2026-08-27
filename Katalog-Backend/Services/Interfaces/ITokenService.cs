namespace Katalog_Backend.Services.Interfaces;

public interface ITokenService
{
    public string CreateToken(string userId, string userEmail, IList<string> userRoles);
}