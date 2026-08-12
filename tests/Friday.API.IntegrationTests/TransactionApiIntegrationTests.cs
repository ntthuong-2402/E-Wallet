using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Friday.API.IntegrationTests;

[Collection("Admin API integration")]
public sealed class TransactionApiIntegrationTests
{
    private const string ChangedPassword = "LedgerAdmin@123456789";

    [Fact]
    public async Task Transaction_endpoints_deny_anonymous_and_unprivileged_users()
    {
        await using PaymentLedgerApiFactory factory = new();
        using HttpClient anonymous = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/transactions/{Guid.NewGuid()}")).StatusCode
        );

        using HttpClient ordinary = factory.CreateClient();
        HttpResponseMessage registered = await ordinary.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"ledger-user-{Guid.NewGuid():N}",
            email = $"ledger-user-{Guid.NewGuid():N}@example.com",
            password = "Ordinary@123456",
            fullName = "Ordinary Ledger User",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        ordinary.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await ReadAccessTokenAsync(registered)
        );

        HttpResponseMessage forbidden = await ordinary.PostAsJsonAsync(
            "/api/ledger/accounts",
            new { refId = "ordinary-account", accountRef = "ordinary-account" }
        );
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Ledger_account_open_is_idempotent_and_VND_is_explicit()
    {
        await using PaymentLedgerApiFactory factory = new();
        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);

        string refId = $"open:{Guid.NewGuid():N}";
        string accountRef = $"ACC-{Guid.NewGuid():N}";
        object request = new
        {
            refId,
            accountRef,
            availableBalance = 999999m,
            status = "Suspended",
            currency = "USD",
        };

        HttpResponseMessage first = await admin.PostAsJsonAsync("/api/ledger/accounts", request);
        HttpResponseMessage replay = await admin.PostAsJsonAsync("/api/ledger/accounts", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        string firstId = await ReadDataStringAsync(first, "id");
        string replayJson = await replay.Content.ReadAsStringAsync();
        using JsonDocument replayBody = JsonDocument.Parse(replayJson);
        Assert.Equal(
            firstId,
            replayBody.RootElement.GetProperty("data").GetProperty("id").ToString()
        );
        Assert.Equal(
            "VND",
            replayBody.RootElement.GetProperty("data").GetProperty("currency").GetString()
        );
        Assert.Equal(
            0m,
            replayBody.RootElement.GetProperty("data").GetProperty("availableBalance").GetDecimal()
        );
        Assert.Equal(
            0,
            replayBody.RootElement.GetProperty("data").GetProperty("status").GetInt32()
        );

        HttpResponseMessage conflict = await admin.PostAsJsonAsync(
            "/api/ledger/accounts",
            new { refId, accountRef = $"ACC-{Guid.NewGuid():N}" }
        );
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        PaymentLedgerDbContext ledgerDb = scope.ServiceProvider
            .GetRequiredService<PaymentLedgerDbContext>();
        Assert.Equal(1, await ledgerDb.FinancialAuditRecords.CountAsync());
    }

    [Fact]
    public async Task Internal_transfer_rejects_non_VND_without_posting()
    {
        await using PaymentLedgerApiFactory factory = new();
        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);

        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/transactions/transfers", new
        {
            refId = $"transfer:{Guid.NewGuid():N}",
            sourceAccountId = Guid.NewGuid(),
            destinationAccountId = Guid.NewGuid(),
            amount = 1000m,
            currency = "USD",
            description = "must be rejected",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_contributes_all_PaymentLedger_permissions()
    {
        await using PaymentLedgerApiFactory factory = new();
        _ = factory.CreateClient();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FridayDbContext db = scope.ServiceProvider.GetRequiredService<FridayDbContext>();
        string[] permissions = await db.Set<Right>()
            .Where(x => x.Code.StartsWith("LEDGER_") || x.Code.StartsWith("TRANSACTIONS_"))
            .Select(x => x.Code)
            .OrderBy(x => x)
            .ToArrayAsync();
        Assert.Equal(5, permissions.Length);
    }

    [Fact]
    public async Task Transaction_write_rate_limit_returns_too_many_requests()
    {
        await using PaymentLedgerApiFactory factory = new();
        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 40).Select(index => admin.PostAsJsonAsync(
                "/api/transactions/transfers",
                new
                {
                    refId = $"rate:{index}",
                    sourceAccountId = Guid.NewGuid(),
                    destinationAccountId = Guid.NewGuid(),
                    amount = 1m,
                    currency = "USD",
                    description = "rate test",
                }
            ))
        );

        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.TooManyRequests);
        foreach (HttpResponseMessage response in responses) response.Dispose();
    }

    private static async Task AuthenticateAdminAsync(
        HttpClient client,
        PaymentLedgerApiFactory factory
    )
    {
        string first = await LoginAsync(client, factory.Username, factory.InitialPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first);
        HttpResponseMessage changed = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = factory.InitialPassword,
            newPassword = ChangedPassword,
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, factory.Username, ChangedPassword)
        );
    }

    private static async Task<string> LoginAsync(HttpClient client, string login, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { login, password }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAccessTokenAsync(response);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> ReadDataStringAsync(
        HttpResponseMessage response,
        string property
    )
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement.GetProperty("data").GetProperty(property).ToString();
    }

    private sealed class PaymentLedgerApiFactory : WebApplicationFactory<Program>
    {
        public string Username { get; }
        public string InitialPassword { get; } = "Bootstrap@123456";

        public PaymentLedgerApiFactory()
        {
            string suffix = Guid.NewGuid().ToString("N")[..10];
            Username = $"ledger-admin-{suffix}";
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("ConnectionStrings__FridayDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__CustomerDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__PaymentLedgerDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__Redis", " ");
            Environment.SetEnvironmentVariable("Database__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable(
                "Database__InMemoryDatabaseName",
                $"Friday.PaymentLedgerIntegration.{suffix}"
            );
            Environment.SetEnvironmentVariable("Cache__UseRedis", "false");
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "false");
            Environment.SetEnvironmentVariable("Authentication__AllowPublicRegistration", "true");
            Environment.SetEnvironmentVariable(
                "Authentication__Jwt__Secret",
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            );
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Enabled", "true");
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__UserCode",
                $"LEDGER_ADMIN_{suffix}".ToUpperInvariant()
            );
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Username", Username);
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__Email",
                $"{Username}@example.com"
            );
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__FullName",
                "Payment Ledger Administrator"
            );
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__InitialPassword",
                InitialPassword
            );
            Environment.SetEnvironmentVariable("Customer__AuditRetention__WorkerEnabled", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Testing");
    }
}
