using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Friday.API.IntegrationTests;

[Collection("Admin API integration")]
public sealed class CustomerApiIntegrationTests
{
    private const string ChangedPassword = "CustomerAdmin@123456789";
    private const string CitizenId = "001234567890";

    [Fact]
    public async Task Customer_host_exposes_live_and_ready_health_checks()
    {
        await using CustomerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Customer_write_rate_limit_returns_too_many_requests()
    {
        await using CustomerApiFactory factory = new();
        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 40).Select(_ => admin.PatchAsJsonAsync(
                "/api/customers/999999/status",
                new { expectedVersion = 0, targetStatus = "Suspended", reason = "rate-test" }
            ))
        );

        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.TooManyRequests);
        foreach (HttpResponseMessage response in responses) response.Dispose();
    }

    [Fact]
    public async Task Customer_readiness_fails_when_customer_database_is_unavailable()
    {
        await using UnavailableCustomerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Customer_endpoints_enforce_permissions_and_never_expose_raw_document()
    {
        await using CustomerApiFactory factory = new();
        using HttpClient anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/customers/1")).StatusCode);

        using HttpClient ordinary = factory.CreateClient();
        HttpResponseMessage registered = await ordinary.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"ordinary-{Guid.NewGuid():N}",
            email = $"ordinary-{Guid.NewGuid():N}@example.com",
            password = "Ordinary@123456",
            fullName = "Ordinary User",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        ordinary.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ReadAccessTokenAsync(registered)
        );
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/customers/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Forbidden Customer",
            dateOfBirth = (string?)null,
            documentType = 1,
            issuingCountryCode = "VN",
            citizenDocumentNumber = "001234567891",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PutAsJsonAsync("/api/customers/1", new
        {
            expectedVersion = 0,
            fullName = "Forbidden Update",
            dateOfBirth = (string?)null,
            documentType = (int?)null,
            issuingCountryCode = (string?)null,
            citizenDocumentNumber = (string?)null,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PatchAsJsonAsync("/api/customers/1/status", new
        {
            expectedVersion = 0,
            targetStatus = "Suspended",
            reason = "forbidden",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/customers/1/audit")).StatusCode);

        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Nguyen Van Customer",
            dateOfBirth = "1990-01-02",
            documentType = 1,
            issuingCountryCode = "VN",
            citizenDocumentNumber = CitizenId,
            customerCode = "CLIENT_MUST_NOT_CONTROL_THIS",
            status = "Closed",
            version = 99,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        string createdJson = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CitizenId, createdJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ciphertext", createdJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lookupHash", createdJson, StringComparison.OrdinalIgnoreCase);

        using JsonDocument createdBody = JsonDocument.Parse(createdJson);
        JsonElement createdData = createdBody.RootElement.GetProperty("data");
        int customerId = createdData.GetProperty("id").GetInt32();
        string generatedCode = createdData.GetProperty("customerCode").GetString()!;
        Assert.StartsWith("CUS_", generatedCode, StringComparison.Ordinal);
        Assert.Equal(0, createdData.GetProperty("version").GetInt64());

        HttpResponseMessage duplicate = await admin.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Duplicate Document",
            dateOfBirth = (string?)null,
            documentType = 1,
            issuingCountryCode = "VN",
            citizenDocumentNumber = CitizenId,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        HttpResponseMessage detail = await admin.GetAsync($"/api/customers/{customerId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain(CitizenId, await detail.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        HttpResponseMessage list = await admin.GetAsync("/api/customers?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("1", list.Headers.GetValues("X-Total-Count").Single());
        Assert.DoesNotContain(CitizenId, await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        HttpResponseMessage audit = await admin.GetAsync($"/api/customers/{customerId}/audit?skip=0&take=10");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        string auditJson = await audit.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CitizenId, auditJson, StringComparison.Ordinal);
        Assert.Contains("CUSTOMER_CREATED", auditJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Customer_update_and_status_change_enforce_expected_version()
    {
        await using CustomerApiFactory factory = new();
        using HttpClient admin = factory.CreateClient();
        await AuthenticateAdminAsync(admin, factory);
        (int id, long version) = await CreateCustomerAsync(admin);

        HttpResponseMessage updated = await admin.PutAsJsonAsync($"/api/customers/{id}", new
        {
            expectedVersion = version,
            fullName = "Updated Customer",
            dateOfBirth = (string?)null,
            documentType = 2,
            issuingCountryCode = "FR",
            citizenDocumentNumber = "12AB3456",
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        long updatedVersion = await ReadDataInt64Async(updated, "version");

        HttpResponseMessage stale = await admin.PatchAsJsonAsync($"/api/customers/{id}/status", new
        {
            expectedVersion = version,
            targetStatus = "Suspended",
            reason = "manual review",
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        HttpResponseMessage changed = await admin.PatchAsJsonAsync($"/api/customers/{id}/status", new
        {
            expectedVersion = updatedVersion,
            targetStatus = "Suspended",
            reason = "manual review",
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_seeds_customer_permissions_for_super_admin()
    {
        await using CustomerApiFactory factory = new();
        _ = factory.CreateClient();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FridayDbContext dbContext = scope.ServiceProvider.GetRequiredService<FridayDbContext>();
        string[] codes = await dbContext.Set<Right>()
            .Where(x => x.Code.StartsWith("CUSTOMERS_"))
            .Select(x => x.Code)
            .OrderBy(x => x)
            .ToArrayAsync();
        Assert.Equal(6, codes.Length);
    }

    private static async Task<(int Id, long Version)> CreateCustomerAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test Customer",
            dateOfBirth = (string?)null,
            documentType = 1,
            issuingCountryCode = "VN",
            citizenDocumentNumber = CitizenId,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        JsonElement data = body.RootElement.GetProperty("data");
        return (data.GetProperty("id").GetInt32(), data.GetProperty("version").GetInt64());
    }

    private static async Task AuthenticateAdminAsync(HttpClient client, CustomerApiFactory factory)
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
            "Bearer", await LoginAsync(client, factory.Username, ChangedPassword)
        );
    }

    private static async Task<string> LoginAsync(HttpClient client, string login, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new { login, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAccessTokenAsync(response);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static async Task<long> ReadDataInt64Async(HttpResponseMessage response, string property)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement.GetProperty("data").GetProperty(property).GetInt64();
    }

    private sealed class CustomerApiFactory : WebApplicationFactory<Program>
    {
        public string Username { get; }
        public string InitialPassword { get; } = "Bootstrap@123456";

        public CustomerApiFactory()
        {
            string suffix = Guid.NewGuid().ToString("N")[..10];
            Username = $"customer-admin-{suffix}";
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("ConnectionStrings__FridayDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__CustomerDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__Redis", " ");
            Environment.SetEnvironmentVariable("Database__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable("Database__InMemoryDatabaseName", $"Friday.CustomerIntegration.{suffix}");
            Environment.SetEnvironmentVariable("Cache__UseRedis", "false");
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "false");
            Environment.SetEnvironmentVariable("Authentication__AllowPublicRegistration", "true");
            Environment.SetEnvironmentVariable("Authentication__Jwt__Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Enabled", "true");
            Environment.SetEnvironmentVariable("Admin__Bootstrap__UserCode", $"CUST_ADMIN_{suffix}".ToUpperInvariant());
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Username", Username);
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Email", $"{Username}@example.com");
            Environment.SetEnvironmentVariable("Admin__Bootstrap__FullName", "Customer Administrator");
            Environment.SetEnvironmentVariable("Admin__Bootstrap__InitialPassword", InitialPassword);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }

    private sealed class UnavailableCustomerApiFactory : WebApplicationFactory<Program>
    {
        public UnavailableCustomerApiFactory()
        {
            string suffix = Guid.NewGuid().ToString("N")[..10];
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("ConnectionStrings__FridayDb", " ");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__CustomerDb",
                "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1"
            );
            Environment.SetEnvironmentVariable("ConnectionStrings__Redis", " ");
            Environment.SetEnvironmentVariable("Database__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable("Database__InMemoryDatabaseName", $"Friday.CustomerHealth.{suffix}");
            Environment.SetEnvironmentVariable("Cache__UseRedis", "false");
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "false");
            Environment.SetEnvironmentVariable("Authentication__Jwt__Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Enabled", "false");
            Environment.SetEnvironmentVariable("Customer__AuditRetention__WorkerEnabled", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
