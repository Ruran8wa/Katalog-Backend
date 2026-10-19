using System.Net;
using System.Text.Json;
using Katalog_Backend.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class LocalDependenciesTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            Environment.GetEnvironmentVariable("CI_DATABASE_CONNECTION_STRING")
            ?? "Server=localhost,1433;Database=KatalogDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;");
        Environment.SetEnvironmentVariable(
            "AzureServiceBus__ConnectionString",
            Environment.GetEnvironmentVariable("CI_SERVICE_BUS_CONNECTION_STRING")
            ?? "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
        Environment.SetEnvironmentVariable("BackgroundServices__ConsumerEnabled", "false");
        Environment.SetEnvironmentVariable("BackgroundServices__OutboxPublisherEnabled", "false");
        Environment.SetEnvironmentVariable("Seeding__Enabled", "false");

        _factory = new WebApplicationFactory<Program>();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task ReadyHealthCheck_ReportsDatabaseAndServiceBusAsHealthy()
    {
        using var response = await _client.GetAsync("/health/ready");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
        Assert.That(
            document.RootElement.GetProperty("entries").GetProperty("database").GetProperty("status").GetString(),
            Is.EqualTo("Healthy"));
        Assert.That(
            document.RootElement.GetProperty("entries").GetProperty("servicebus").GetProperty("status").GetString(),
            Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task GetCategories_UsesTheLocalDatabase()
    {
        using var response = await _client.GetAsync("/api/category");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task GetVersion_ReturnsTheBuiltVersion()
    {
        using var response = await _client.GetAsync("/version");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("version").GetString(), Is.Not.Null.And.Not.Empty);
    }
}
