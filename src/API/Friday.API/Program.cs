using System.Text;
using Friday.API.Common;
using Friday.API.Configuration;
using Friday.API.Middlewares;
using Friday.API.Modules.Admin;
using Friday.API.Modules.Auth;
using Friday.API.Modules.Sample;
using Friday.BuildingBlocks.Application;
using Friday.BuildingBlocks.Infrastructure;
using Friday.BuildingBlocks.Infrastructure.Hosting;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Infrastructure;
using Friday.Modules.Sample.Application;
using Friday.Modules.Sample.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
    builder.Services.AddSampleApplication();
    builder.Services.AddSampleInfrastructure();

    builder.Services.AddHttpContextAccessor();

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
    app.UseAuthorization();
    app.UseMiddleware<AuthenticatedUserValidationMiddleware>();

    app.MapGet(
        "/",
        (HttpContext context) => ApiResults.Ok(context, "Friday modular monolith is running.")
    );
    app.MapAuthModule();
    app.MapAdminModule();
    app.MapSampleModule();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
