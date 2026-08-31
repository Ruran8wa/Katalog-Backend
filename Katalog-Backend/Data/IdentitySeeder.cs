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
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == "DuplicateRoleName"))
                    continue;

                var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
            }
        }

    }
}