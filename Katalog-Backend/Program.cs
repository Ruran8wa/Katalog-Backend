using System.Text;
using System.Diagnostics;
using dotenv.net;
using Katalog_Backend.BackgroundServices;
using Katalog_Backend.Configuration;
using Katalog_Backend.Data;
using Katalog_Backend.HealthChecks;
using Katalog_Backend.Middleware;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Katalog_Backend.Observability;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 4));

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration.GetValue<bool>("BackgroundServices:ConsumerEnabled")
    ? "order-consumer"
    : "inventory-api";
var serviceVersion = builder.Configuration["APP_VERSION"] ?? "unknown";
var deploymentEnvironment = builder.Configuration["ASPNETCORE_ENVIRONMENT"]
    ?? Environments.Production;
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = deploymentEnvironment
    });

builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.ParseStateValues = true;
    options.AddConsoleExporter();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = deploymentEnvironment
        }))
    .WithTracing(tracing => tracing
        .AddSource(Telemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = httpContext =>
                !httpContext.Request.Path.StartsWithSegments("/health/live") &&
                !httpContext.Request.Path.StartsWithSegments("/health/ready");
        })
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(Telemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo{ Title = "Katalog-Backend", Version = "v1"});

    options.AddSecurityDefinition(
        "Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the token returned from /api/auth/login."
        });
        
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    } );
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryRepo, CategoryRepo>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IVariantRepository, VariantRepository>();
builder.Services.AddScoped<IVariantService, VariantService>();
builder.Services.AddScoped<IFileStorageService, CloudinaryStorageService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();

builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection(AzureServiceBusOptions.SectionName));
var backgroundServiceOptions = builder.Configuration
    .GetSection(BackgroundServiceOptions.SectionName)
    .Get<BackgroundServiceOptions>() ?? new BackgroundServiceOptions();

if (backgroundServiceOptions.OutboxPublisherEnabled)
{
    builder.Services.AddHostedService<OutboxPublisherWorker>();
}

if (backgroundServiceOptions.ConsumerEnabled)
{
    builder.Services.AddHostedService<OrderStockDecrementConsumer>();
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<ServiceBusHealthCheck>("servicebus", tags: ["ready"]);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ??
                      throw new InvalidOperationException("Jwt:Issuer is not configured."),
        ValidAudience = builder.Configuration["Jwt:Audience"] ??
                        throw new InvalidOperationException("Jwt:Audience is not configured."),
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ??
                                   throw new InvalidOperationException("Jwt:Key is not configured.")))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

Telemetry.SetPendingOrdersObserver(() =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    return db.Orders.Count(order => order.Status == Katalog_Backend.Enums.OrderStatus.Pending);
});

app.MapGet("/version", (IConfiguration configuration) =>
    Results.Ok(new
    {
        version = configuration["APP_VERSION"] ?? "unknown"
    }));

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using var traceScope = Telemetry.BeginTraceScope(logger);
    var startedAt = Stopwatch.GetTimestamp();
    try
    {
        await next();
    }
    finally
    {
        var endpoint = context.GetEndpoint()?.DisplayName ?? context.Request.Path.ToString();
        var tags = new TagList { { "http.route", endpoint } };
        Telemetry.HttpRequests.Add(1, tags);
        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            Telemetry.HttpErrors.Add(1, tags);
        }

        Telemetry.HttpRequestDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            tags);
    }
});

var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation(
    "Background service roles: OutboxPublisher={OutboxStatus}, Consumer={ConsumerStatus}",
    backgroundServiceOptions.OutboxPublisherEnabled ? "Enabled" : "Disabled",
    backgroundServiceOptions.ConsumerEnabled ? "Enabled" : "Disabled");

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapControllers();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var seedingEnabled = builder.Configuration.GetValue<bool?>("Seeding:Enabled") ?? true;

if (seedingEnabled)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding Identity roles.");
    }
}

app.Run();
