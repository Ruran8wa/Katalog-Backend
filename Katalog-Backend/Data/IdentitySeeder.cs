using Microsoft.AspNetCore.Identity;

namespace Katalog_Backend.Data;

public static class IdentitySeeder
{
    private static readonly string[] Roles = ["Admin", "Customer"];

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
