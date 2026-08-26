using System.ComponentModel.DataAnnotations;
using Katalog_Backend.DTO;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Auth;
[TestFixture]
public class RegisterDtoShould
{
    private RegisterDto _registerDto;
    
    [SetUp]
    public void Setup()
    {
       var createDto = new RegisterDto
       {
           FirstName = "Prince", 
           LastName = "Rurangwa", 
           Email = "princerurangwa@gmail.com", 
           Password = "12345678"
       };
       _registerDto = createDto;
    }
    
    [Test]
    public void RegisterDtoMissingField()
    {
        _registerDto.FirstName = null;
        var isValid = ValidateRegisterDto(_registerDto);
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void RegisterDtoInvalidEmail()
    {
        _registerDto.Email = "testemail.com";
        var isValid = ValidateRegisterDto(_registerDto);
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void RegisterDtoInvalidPassword()
    {
        _registerDto.Password = "1234567";
        var isValid = ValidateRegisterDto(_registerDto);
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void RegisterDtoSuccessfully()
    {
        var isValid = ValidateRegisterDto(_registerDto);
        Assert.That(isValid, Is.True);
    }
    
    private bool ValidateRegisterDto(RegisterDto model)
    {
        var context = new ValidationContext(model);
        var result = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, result, true);
    }
}