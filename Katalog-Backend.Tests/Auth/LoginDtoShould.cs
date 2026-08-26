using System.ComponentModel.DataAnnotations;
using Katalog_Backend.DTO;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Auth;

[TestFixture]
public class LoginDtoShould
{
    public LoginDto _loginDto;

    [SetUp]
    public void Setup()
    {
        var loginDto = new LoginDto{Email = "princerurangwa@gmail.com", Password = "password"};
        _loginDto = loginDto;
    }

    [Test]
    public void LoginDtoSuccessfully()
    {
        var isValid = ValidateLoginDto(_loginDto);
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void LoginDtoWrongEmail()
    {
        _loginDto.Email = null;
        var isValid = ValidateLoginDto(_loginDto);
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void LoginDtoWrongPassword()
    {
        _loginDto.Password = null;
        var isValid = ValidateLoginDto(_loginDto);
        Assert.That(isValid, Is.False);
    }

    private bool ValidateLoginDto(LoginDto loginDto)
    {
        var context = new ValidationContext(loginDto);
        var result =  new List<ValidationResult>();
        return Validator.TryValidateObject(loginDto, context, result, true);
    }
}