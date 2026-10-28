using System.Net.Http.Headers;
using System.Net.Http.Json;
using Katalog_Backend.Data;
using Katalog_Backend.DTO;
using Katalog_Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Katalog_Backend.Tests.Helpers;

public sealed record TestCaller(HttpClient Client, string UserId);

// Hosts the API against the KatalogTests database with background workers off.
public sealed class KatalogApiFactory : WebApplicationFactory<Program>
{
    private const string Password = "Test-Passw0rd!";

    public static KatalogApiFactory Create()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable(
            "AzureServiceBus__ConnectionString",
            Environment.GetEnvironmentVariable("CI_SERVICE_BUS_CONNECTION_STRING")
            ?? "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
        Environment.SetEnvironmentVariable("BackgroundServices__ConsumerEnabled", "false");
        Environment.SetEnvironmentVariable("BackgroundServices__OutboxPublisherEnabled", "false");
        Environment.SetEnvironmentVariable("Seeding__Enabled", "true");
        Environment.SetEnvironmentVariable("Jwt__Key", "katalog-tests-signing-key-at-least-32-bytes-long!");

        TestDatabase.EnsureMigrated();
        return new KatalogApiFactory();
    }

    public HttpClient CreateAnonymousClient() => CreateClient();

    public Task<TestCaller> CreateCustomerAsync() => CreateCallerAsync("Customer");

    public Task<TestCaller> CreateAdminAsync() => CreateCallerAsync("Admin");

    private async Task<TestCaller> CreateCallerAsync(string role)
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        string userId;
        using (var scope = Services.CreateScope())
        {
            await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Test", LastName = role };
            var created = await userManager.CreateAsync(user, Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, role);
            userId = user.Id;
        }

        var client = CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>()
                   ?? throw new InvalidOperationException("Login returned no body.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new TestCaller(client, userId);
    }
}
