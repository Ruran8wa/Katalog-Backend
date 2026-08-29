using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Services;
using Katalog_Backend.Services.Interfaces;
using Katalog_Backend.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class AuthService_Test
{
    private Mock<UserManager<ApplicationUser>> _userManagerMock;
    private Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private Mock<ITokenService> _tokenServiceMock;
    private AuthService _authService;

    [SetUp]
    public void Setup()
    {
        _userManagerMock = MockHelper.MockUserManager<ApplicationUser>();
        _signInManagerMock = MockHelper.MockSignInManager<ApplicationUser>(_userManagerMock);
        _tokenServiceMock = new Mock<ITokenService>();
        
        _authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _tokenServiceMock.Object);
    }

    [Test]
    public async Task RegisterAsync_ValidRequest_RegistersUser()
    {
        var request = new RegisterDto
        {
            Email = "test@gmail.com",
            FirstName = "test",
            LastName = "test",
            Password = "Password!123"
        };
        
        var token = "fake-jwt-token-string";
        
        
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _tokenServiceMock.Setup(t => t.CreateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>())).Returns(token);

        var register = await _authService.RegisterAsync(request);
        
        Assert.That(register, Is.Not.Null);
        Assert.That(register.Token, Is.Not.Null);
        _userManagerMock.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u => u.Email == request.Email && u.FirstName == request.FirstName), request.Password), Times.Once);
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Email == request.Email), It.IsAny<string>()), Times.Once);
        _tokenServiceMock.Verify(t => t.CreateToken(It.IsAny<string>(), request.Email, It.IsAny<IList<string>>()), Times.Once);
    }
    
    [Test]
    public async Task RegisterAsync_CreateAsyncFails_ThrowsRegistrationException()
    {
        var request = new RegisterDto
        {
            Email = "duplicate@gmail.com",
            FirstName = "test",
            LastName = "test",
            Password = "weak"
        };
        
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed());

        Assert.ThrowsAsync<RegistrationException>( async () => await _authService.RegisterAsync(request));
        
        _userManagerMock.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u => u.Email == request.Email && u.FirstName == request.FirstName), request.Password), Times.Once);
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Email == request.Email), It.IsAny<string>()), Times.Never);
        _tokenServiceMock.Verify(t => t.CreateToken(It.IsAny<string>(), request.Email, It.IsAny<IList<string>>()), Times.Never);
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtToken()
    {
        var request = new LoginDto { Email = "test@katalog.com", Password = "Passw0rd!" };
        var user = new ApplicationUser { Email = request.Email, UserName = request.Email };
        var roles = new List<string> {"Customer"};
        var expectedToken = "fake-jwt-token-string";

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, request.Password, true)).ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);
        _tokenServiceMock.Setup(t => t.CreateToken(user.Id, user.Email, roles)).Returns(expectedToken);
        
        var result = await _authService.LoginAsync(request);
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Token, Is.EqualTo(expectedToken));
        
        _userManagerMock.Verify(x => x.FindByEmailAsync(request.Email), Times.Once);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, request.Password, true), Times.Once);
        _userManagerMock.Verify(x => x.GetRolesAsync(user), Times.Once);
        _tokenServiceMock.Verify(x => x.CreateToken(user.Id, user.Email, roles), Times.Once);

    }
    
    [Test]
    public async Task LoginAsync_UserNotFound_ThrowsInvalidCredentialsException()
    {
        var request = new LoginDto { Email = "test@katalog.com", Password = "Password!123" };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync((ApplicationUser?)null);
        
        Assert.ThrowsAsync<InvalidCredentialsException>(async () => await _authService.LoginAsync(request));
        
        _userManagerMock.Verify(x => x.FindByEmailAsync(request.Email), Times.Once);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _tokenServiceMock.Verify(x => x.CreateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>()), Times.Never);

    }

    [Test]
    public async Task LoginAsync_InvalidPassword_ThrowsInvalidCredentialException()
    {
        var request = new LoginDto {Email = "test@katalog.com", Password = "WrongPassword!123"};
        var user = new ApplicationUser { Email = request.Email, UserName = request.Email };
        
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, request.Password, true)).ReturnsAsync(SignInResult.Failed);
        
        Assert.ThrowsAsync<InvalidCredentialsException>(async () => await _authService.LoginAsync(request));
        
        _userManagerMock.Verify(x => x.FindByEmailAsync(request.Email), Times.Once);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, request.Password, true), Times.Once);
        
        _userManagerMock.Verify(x => x.GetRolesAsync(user), Times.Never);
        _tokenServiceMock.Verify(x => x.CreateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>()), Times.Never);
    }
}