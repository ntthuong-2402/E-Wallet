using System.Text;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Friday.API.Common;
using Friday.API.Configuration;
using Friday.API.Middlewares;
using Friday.API.Modules.Admin;
using Friday.API.Modules.Auth;
using Friday.API.Modules.Sample;
using Friday.API.Modules.Customer;
using Friday.API.Modules.PaymentLedger;
using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Application.Customers;
using Friday.BuildingBlocks.Application;
using Friday.BuildingBlocks.Infrastructure;
using Friday.BuildingBlocks.Infrastructure.Hosting;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Infrastructure;
using Friday.Modules.Admin.Infrastructure.Bootstrap;
using Friday.Modules.Customer.Application;
using Friday.Modules.Customer.Infrastructure;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Friday.Modules.PaymentLedger.Application;
using Friday.Modules.PaymentLedger.Application.Actors;
using Friday.Modules.PaymentLedger.Infrastructure;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Friday.Modules.Sample.Application;
using Friday.Modules.Sample.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.AddFridaySerilog();

    builder.Services.AddFridayOpenTelemetry(builder.Configuration);

    builder.Services.AddBuildingBlocksApplication();
    builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration);
    builder.Services.Configure<LocalizationOptions>(
        builder.Configuration.GetSection("Localization")
    );
    builder.Services.Configure<RegistrationOptions>(
        builder.Configuration.GetSection(RegistrationOptions.SectionName)
    );
    builder.Services.AddScoped<IErrorMessageLocalizer, ErrorMessageLocalizer>();
    builder.Services.AddLinKitCqrs();
    builder.Services.AddAdminApplication();
    builder.Services.AddAdminInfrastructure(builder.Configuration);
    builder.Services.AddCustomerApplication();
    builder.Services.AddCustomerInfrastructure(builder.Configuration);
    builder.Services.AddPaymentLedgerApplication();
    builder.Services.AddPaymentLedgerInfrastructure(builder.Configuration);
    builder.Services.AddSampleApplication();
    builder.Services.AddSampleInfrastructure();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICustomerActor, HttpCustomerActor>();
    builder.Services.AddScoped<IExternalAccountDirectory, AdminExternalAccountDirectory>();
    builder.Services.AddScoped<IPaymentLedgerActor, HttpPaymentLedgerActor>();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth-strict", limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });
        options.AddPolicy("customer-read", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            )
        );
        options.AddPolicy("customer-write", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            )
        );
        options.AddPolicy("transaction-read", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            )
        );
        options.AddPolicy("transaction-write", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            )
        );
    });

    JwtSettings jwtBind =
        builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
        ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(jwtBind.Secret) || jwtBind.Secret.Length < 32)
    {
        throw new InvalidOperationException(
            "Configure Authentication:Jwt:Secret with at least 32 characters (see appsettings)."
        );
    }

    builder
        .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtBind.Issuer,
                ValidAudience = jwtBind.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtBind.Secret)),
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddFridaySwagger();

    WebApplication app = builder.Build();
    ApplicationServiceProviderAccessor.SetRoot(app.Services);

    Microsoft.Extensions.Logging.ILogger startupLogger = app.Logger;
    startupLogger.LogInformation("Friday.API build complete; running database migrations if enabled.");
    await app.Services.ApplyEfThenDataMigrationsAsync(app.Configuration);
    await app.Services.ApplyCustomerMigrationsAsync(app.Configuration);
    await app.Services.ApplyPaymentLedgerMigrationsAsync(app.Configuration);
    await using (AsyncServiceScope bootstrapScope = app.Services.CreateAsyncScope())
    {
        await bootstrapScope.ServiceProvider
            .GetRequiredService<AdminSecurityBootstrapper>()
            .ApplyAsync();
    }
    startupLogger.LogInformation("Database migration step finished; configuring HTTP pipeline.");

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set(
                "TraceId",
                System.Diagnostics.Activity.Current?.TraceId.ToString()
                    ?? httpContext.TraceIdentifier
            );
        };

        options.GetLevel = (httpContext, elapsed, exception) =>
        {
            if (exception is not null || httpContext.Response.StatusCode >= 500)
            {
                return LogEventLevel.Error;
            }

            if (elapsed > 500)
            {
                return LogEventLevel.Warning;
            }

            return LogEventLevel.Information;
        };
    });
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Friday API v1");
        });
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseMiddleware<AuthenticatedUserValidationMiddleware>();
    app.UseAuthorization();

    app.MapGet(
        "/",
        (HttpContext context) => ApiResults.Ok(context, "Friday modular monolith is running.")
    );
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
    }).AllowAnonymous();
    app.MapAuthModule();
    app.MapAdminModule();
    app.MapCustomerModule();
    app.MapPaymentLedgerModule();
    app.MapSampleModule();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
