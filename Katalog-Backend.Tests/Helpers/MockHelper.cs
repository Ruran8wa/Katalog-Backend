using Katalog_Backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Katalog_Backend.Tests.Helpers;

public class MockHelper
{
    public static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var mgr = new Mock<UserManager<TUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        
        mgr.Object.UserValidators.Add(new UserValidator<TUser>());
        mgr.Object.PasswordValidators.Add(new PasswordValidator<TUser>());

        return mgr;
    }

    public static Mock<SignInManager<TUser>> MockSignInManager<TUser>(Mock<UserManager<TUser>> userManagerMock) where TUser : class
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimFactory = new Mock<IUserClaimsPrincipalFactory<TUser>>();

        return new Mock<SignInManager<TUser>>(
            userManagerMock.Object,
            contextAccessor.Object,
            claimFactory.Object,
            null, null, null, null
        );
    }
}