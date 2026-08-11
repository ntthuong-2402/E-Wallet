using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Friday.Modules.Admin.Domain.Security;
using Xunit;

namespace Friday.API.IntegrationTests;

[Collection("Admin API integration")]
public sealed class AdminSecurityIntegrationTests
{
    private const string ChangedPassword = "Changed@123456789";

    [Fact]
    public async Task First_start_provisions_super_admin_that_must_change_password()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FridayDbContext dbContext = scope.ServiceProvider.GetRequiredService<FridayDbContext>();
        User user = await dbContext
            .Set<User>()
            .Include(x => x.PasswordCredential)
            .Include(x => x.UserRoles)
            .SingleAsync(x => x.UserCode == factory.UserCode);
        Role role = await dbContext.Set<Role>().SingleAsync(x => x.Code == "SUPER_ADMIN");

        Assert.True(user.MustChangePassword);
        Assert.Contains(user.UserRoles, x => x.RoleId == role.Id);
        Assert.NotNull(user.PasswordCredential);

        IPasswordHasher<CredentialUser> hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<CredentialUser>>();
        PasswordVerificationResult verification = hasher.VerifyHashedPassword(
            new CredentialUser(),
            user.PasswordCredential!.PasswordHash,
            factory.InitialPassword
        );
        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public async Task First_login_is_restricted_until_password_is_changed()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        string firstToken = await LoginAsync(client, factory.Username, factory.InitialPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            firstToken
        );

        HttpResponseMessage blocked = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        HttpResponseMessage changed = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = factory.InitialPassword, newPassword = ChangedPassword }
        );
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        HttpResponseMessage revokedSession = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSession.StatusCode);

        string newToken = await LoginAsync(client, factory.Username, ChangedPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            newToken
        );
        HttpResponseMessage allowed = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_without_permission_cannot_access_admin_endpoint()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "ordinary-user",
                email = "ordinary@example.com",
                password = "Ordinary@123456",
                fullName = "Ordinary User",
                phone = (string?)null,
            }
        );
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        string token = await ReadAccessTokenAsync(registered);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refresh_allows_only_one_rotation()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = $"refresh-{Guid.NewGuid():N}",
                email = $"refresh-{Guid.NewGuid():N}@example.com",
                password = "Refresh@123456",
                fullName = "Refresh User",
                phone = (string?)null,
            }
        );
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        LoginTokens tokens = await ReadLoginTokensAsync(registered);

        Task<HttpResponseMessage> first = client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        Task<HttpResponseMessage> second = client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Role_removal_revokes_access_and_protects_last_super_admin()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        await AuthenticateAdminAsync(client, factory);

        HttpResponseMessage rolesResponse = await client.GetAsync("/api/admin/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        int superAdminRoleId = await FindRoleIdAsync(rolesResponse, "SUPER_ADMIN");
        string secondUsername = $"second-{Guid.NewGuid():N}";
        const string secondInitialPassword = "SecondAdmin@123456";
        const string secondChangedPassword = "SecondChanged@123456";

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/admin/users",
            new
            {
                userCode = $"SECOND_{Guid.NewGuid():N}"[..20],
                username = secondUsername,
                email = $"second-{Guid.NewGuid():N}@example.com",
                fullName = "Second Administrator",
                password = secondInitialPassword,
                phone = (string?)null,
                address = (string?)null,
                companyName = (string?)null,
                jobTitle = (string?)null,
                notes = (string?)null,
                roleIds = new[] { superAdminRoleId },
            }
        );
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        int secondUserId = await ReadDataIntAsync(created, "id");

        using HttpClient secondClient = factory.CreateClient();
        string secondFirstToken = await LoginAsync(
            secondClient,
            secondUsername,
            secondInitialPassword
        );
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            secondFirstToken
        );
        HttpResponseMessage secondChanged = await secondClient.PostAsJsonAsync(
            "/api/auth/change-password",
            new
            {
                currentPassword = secondInitialPassword,
                newPassword = secondChangedPassword,
            }
        );
        Assert.Equal(HttpStatusCode.OK, secondChanged.StatusCode);
        string secondToken = await LoginAsync(
            secondClient,
            secondUsername,
            secondChangedPassword
        );
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            secondToken
        );

        HttpResponseMessage removed = await client.DeleteAsync(
            $"/api/admin/users/{secondUserId}/roles/{superAdminRoleId}"
        );
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        HttpResponseMessage revoked = await secondClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        int systemAdminId = await GetSystemAdminIdAsync(factory);
        HttpResponseMessage protectedResponse = await client.DeleteAsync(
            $"/api/admin/users/{systemAdminId}/roles/{superAdminRoleId}"
        );
        Assert.Equal(HttpStatusCode.Conflict, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task User_list_is_bounded_filterable_and_reports_total_count()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        await AuthenticateAdminAsync(client, factory);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/admin/users?skip=0&take=1&search={Uri.EscapeDataString(factory.Username)}"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1", response.Headers.GetValues("X-Take").Single());
        Assert.Equal("1", response.Headers.GetValues("X-Total-Count").Single());
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task User_update_is_queryable_in_security_audit()
    {
        await using AdminApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        await AuthenticateAdminAsync(client, factory);
        int userId = await GetSystemAdminIdAsync(factory);

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"/api/admin/users/{userId}",
            new
            {
                userCode = factory.UserCode,
                username = factory.Username,
                email = $"{factory.Username}@example.com",
                fullName = "Updated System Administrator",
                phone = (string?)null,
                address = (string?)null,
                companyName = (string?)null,
                jobTitle = (string?)null,
                notes = (string?)null,
            }
        );
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        HttpResponseMessage audit = await client.GetAsync(
            "/api/admin/audit-events?eventType=USER_UPDATED&take=10"
        );
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        await using Stream stream = await audit.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        JsonElement item = Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal("USER_UPDATED", item.GetProperty("eventType").GetString());
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string login,
        string password
    )
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
        => (await ReadLoginTokensAsync(response)).AccessToken;

    private static async Task<LoginTokens> ReadLoginTokensAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        JsonElement data = body.RootElement.GetProperty("data");
        return new LoginTokens(
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("refreshToken").GetString()!
        );
    }

    private static async Task AuthenticateAdminAsync(HttpClient client, AdminApiFactory factory)
    {
        string firstToken = await LoginAsync(client, factory.Username, factory.InitialPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        HttpResponseMessage changed = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = factory.InitialPassword, newPassword = ChangedPassword }
        );
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        string token = await LoginAsync(client, factory.Username, ChangedPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<int> FindRoleIdAsync(HttpResponseMessage response, string roleCode)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Single(x => x.GetProperty("code").GetString() == roleCode)
            .GetProperty("id")
            .GetInt32();
    }

    private static async Task<int> ReadDataIntAsync(HttpResponseMessage response, string property)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument body = await JsonDocument.ParseAsync(stream);
        return body.RootElement.GetProperty("data").GetProperty(property).GetInt32();
    }

    private static async Task<int> GetSystemAdminIdAsync(AdminApiFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FridayDbContext dbContext = scope.ServiceProvider.GetRequiredService<FridayDbContext>();
        return await dbContext
            .Set<User>()
            .Where(x => x.UserCode == factory.UserCode)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private sealed record LoginTokens(string AccessToken, string RefreshToken);

    private sealed class AdminApiFactory : WebApplicationFactory<Program>
    {
        public string UserCode { get; }
        public string Username { get; }
        public string InitialPassword { get; } = "Bootstrap@123456";

        public AdminApiFactory()
        {
            string suffix = Guid.NewGuid().ToString("N")[..10];
            UserCode = $"ADMIN_{suffix}".ToUpperInvariant();
            Username = $"admin-{suffix}";

            // Program composes services and runs bootstrap before WebApplicationFactory's
            // configuration callback. Establish the isolated test configuration first.
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("ConnectionStrings__FridayDb", " ");
            Environment.SetEnvironmentVariable("ConnectionStrings__Redis", " ");
            Environment.SetEnvironmentVariable("Database__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable(
                "Database__InMemoryDatabaseName",
                $"Friday.AdminIntegration.{suffix}"
            );
            Environment.SetEnvironmentVariable("Cache__UseRedis", "false");
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "false");
            Environment.SetEnvironmentVariable("Authentication__AllowPublicRegistration", "true");
            Environment.SetEnvironmentVariable(
                "Authentication__Jwt__Secret",
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            );
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Enabled", "true");
            Environment.SetEnvironmentVariable("Admin__Bootstrap__UserCode", UserCode);
            Environment.SetEnvironmentVariable("Admin__Bootstrap__Username", Username);
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__Email",
                $"{Username}@example.com"
            );
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__FullName",
                "System Administrator"
            );
            Environment.SetEnvironmentVariable(
                "Admin__Bootstrap__InitialPassword",
                InitialPassword
            );
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}

[CollectionDefinition("Admin API integration", DisableParallelization = true)]
public sealed class AdminApiIntegrationCollection;
