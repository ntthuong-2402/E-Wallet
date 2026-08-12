using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Infrastructure.Auditing;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Friday.Modules.Customer.Infrastructure.Security;
using Friday.Modules.Customer.Application.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Friday.Modules.Customer.UnitTests;

public sealed class CustomerPhaseTwoTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CitizenDocument_NormalizesVietnamCitizenId()
    {
        CitizenDocument first = CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId,
            "vn",
            "001 234 567 890"
        );
        CitizenDocument second = CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId,
            "VN",
            "001234567890"
        );

        Assert.Equal(first.Number, second.Number);
        Assert.Equal("001234567890", first.Number);
        Assert.Equal("********7890", first.MaskedValue);
    }

    [Fact]
    public void CitizenDocument_SupportsCountryScopedForeignPassport()
    {
        CitizenDocument french = CitizenDocument.Create(
            CitizenDocumentType.Passport,
            "fr",
            "12-ab 3456"
        );
        CitizenDocument canadian = CitizenDocument.Create(
            CitizenDocumentType.Passport,
            "CA",
            "12AB3456"
        );

        Assert.Equal("FR", french.IssuingCountryCode);
        Assert.Equal("****3456", french.MaskedValue);
        Assert.Equal(french.Number, canadian.Number);
        Assert.NotEqual(french.IssuingCountryCode, canadian.IssuingCountryCode);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("12345678901A")]
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢")]
    public void CitizenProtector_RejectsInvalidVietnamCitizenId(string value)
    {
        Assert.Throws<ArgumentException>(() => CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId,
            "VN",
            value
        ));
    }

    [Fact]
    public void CitizenProtector_RejectsUnsupportedPassportCharacters()
    {
        Assert.Throws<ArgumentException>(() => CitizenDocument.Create(
            CitizenDocumentType.Passport,
            "FR",
            "12/AB3456"
        ));
    }

    [Fact]
    public void CitizenDocument_RejectsNonAsciiPassportCharacters()
    {
        Assert.Throws<ArgumentException>(() => CitizenDocument.Create(
            CitizenDocumentType.Passport,
            "FR",
            "ABCDÉ123"
        ));
    }

    [Fact]
    public void Customer_NormalizesFullNameWhitespace_AndRejectsControlCharacters()
    {
        CitizenDocument document = CitizenDocument.Create(
            CitizenDocumentType.Passport, "FR", "NORMAL123"
        );
        CustomerAggregate customer = CustomerAggregate.Create(
            "CUS_NORMAL_NAME_001", "  Test   Customer  ", null, document, Now
        );

        Assert.Equal("Test Customer", customer.FullName);
        Assert.Throws<ArgumentException>(() => CustomerAggregate.Create(
            "CUS_BAD_NAME_00001", "Bad\0Name", null, document, Now
        ));
    }

    [Fact]
    public void Customer_EnforcesLifecycleAndClosedIsTerminal()
    {
        CustomerAggregate customer = CreateCustomer();

        customer.Suspend("manual review");
        customer.Reactivate("review passed");
        customer.Close("relationship ended");

        Assert.Equal(CustomerStatus.Closed, customer.Status);
        Assert.Equal(3, customer.Version);
        Assert.Throws<InvalidOperationException>(() => customer.Reactivate("not allowed"));
        Assert.Throws<InvalidOperationException>(() => customer.UpdateProfile(
            "Changed",
            null,
            CitizenDocument.Create(CitizenDocumentType.Passport, "FR", "12AB3456")
        ));
    }

    [Fact]
    public void Customer_StatusChangeRequiresReason_AndRejectsInvalidTransition()
    {
        CustomerAggregate customer = CreateCustomer();

        Assert.Throws<ArgumentException>(() => customer.Suspend(" "));
        Assert.Throws<InvalidOperationException>(() => customer.Reactivate("already active"));
    }

    [Fact]
    public void Customer_RequiresFullName()
    {
        CitizenDocument document = CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId, "VN", "001234567890"
        );
        Assert.Throws<ArgumentException>(() => CustomerAggregate.Create(
            "CUS_0123456789ABCDEFG", " ", null, document, Now
        ));
    }

    [Fact]
    public void AuditChangeSet_MasksSensitiveCustomerFields()
    {
        CustomerAggregate customer = CreateCustomer();
        string json = CustomerAuditChangeSet.Created(customer).Json;

        Assert.DoesNotContain("Test Customer", json, StringComparison.Ordinal);
        Assert.DoesNotContain("001234567890", json, StringComparison.Ordinal);
        Assert.Contains("********7890", json, StringComparison.Ordinal);
        Assert.Contains("T************", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerPermissionContribution_DeclaresAllCustomerPolicies()
    {
        CustomerPermissionContribution contribution = new();
        Assert.Equal(8, contribution.Permissions.Count);
        Assert.Contains(CustomerPermissions.Create, contribution.Permissions);
        Assert.Contains(CustomerPermissions.AuditRead, contribution.Permissions);
        Assert.Contains(CustomerPermissions.AccountLinkageManage, contribution.Permissions);
    }

    [Fact]
    public void CustomerCodeGenerator_UsesRequiredPrefixAndAtLeastEightyRandomBits()
    {
        SecureCustomerCodeGenerator generator = new();
        HashSet<string> values = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToHashSet();

        Assert.Equal(100, values.Count);
        Assert.All(values, value =>
        {
            Assert.StartsWith("CUS_", value, StringComparison.Ordinal);
            Assert.Equal(20, value.Length);
        });
    }

    [Fact]
    public void AuditRetention_DefaultsToOneYearAndCanBeConfigured()
    {
        CustomerAuditRetentionPolicy defaultPolicy = new(Options.Create(new CustomerAuditRetentionOptions()));
        CustomerAuditRetentionPolicy configuredPolicy = new(Options.Create(
            new CustomerAuditRetentionOptions { RetentionDays = 30 }
        ));

        Assert.Equal(Now.AddDays(365), defaultPolicy.GetRetainUntilUtc(Now));
        Assert.Equal(Now.AddDays(30), configuredPolicy.GetRetainUntilUtc(Now));
    }

    [Fact]
    public async Task CustomerAudit_IsAppendOnly()
    {
        DbContextOptions<CustomerDbContext> options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseInMemoryDatabase($"CustomerPhaseTwo-{Guid.NewGuid():N}")
            .Options;
        await using CustomerDbContext dbContext = new(options);
        CustomerAggregate customer = CreateCustomer();
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        CustomerChangeAudit audit = CustomerChangeAudit.Create(
            customer,
            "CustomerCreated",
            "operator-1",
            CustomerAuditChangeSet.Created(customer),
            null,
            CustomerStatus.Active.ToString(),
            null,
            "Succeeded",
            "trace-1",
            Now,
            Now.AddDays(365)
        );
        dbContext.CustomerChangeAudits.Add(audit);
        await dbContext.SaveChangesAsync();

        dbContext.CustomerChangeAudits.Remove(audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditRetentionWorker_DryRun_DoesNotDeleteExpiredAudit()
    {
        ServiceCollection services = new();
        string databaseName = $"CustomerRetention-{Guid.NewGuid():N}";
        services.AddDbContext<CustomerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            CustomerDbContext db = seedScope.ServiceProvider.GetRequiredService<CustomerDbContext>();
            CustomerAggregate customer = CreateCustomer();
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            db.CustomerChangeAudits.Add(CustomerChangeAudit.Create(
                customer,
                "CUSTOMER_CREATED",
                "operator-1",
                CustomerAuditChangeSet.Created(customer),
                null,
                CustomerStatus.Active.ToString(),
                null,
                "SUCCESS",
                "trace-dry-run",
                Now.AddDays(-2),
                Now.AddDays(-1)
            ));
            await db.SaveChangesAsync();
        }

        CustomerAuditRetentionWorker worker = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CustomerAuditRetentionOptions
            {
                WorkerEnabled = true,
                DryRun = true,
            }),
            TimeProvider.System,
            new ConfigurationManager(),
            NullLogger<CustomerAuditRetentionWorker>.Instance
        );
        await worker.RunOnceAsync(CancellationToken.None);

        await using AsyncServiceScope verifyScope = provider.CreateAsyncScope();
        CustomerDbContext verification =
            verifyScope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        Assert.Equal(1, await verification.CustomerChangeAudits.CountAsync());
    }

    [Fact]
    public void CustomerModel_HasUniqueCodeAndPlaintextDocumentIndexes()
    {
        DbContextOptions<CustomerDbContext> options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseInMemoryDatabase($"CustomerModel-{Guid.NewGuid():N}")
            .Options;
        using CustomerDbContext dbContext = new(options);
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity =
            dbContext.Model.FindEntityType(typeof(CustomerAggregate))!;

        Assert.Contains(entity.GetIndexes(), x =>
            x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(CustomerAggregate.CustomerCode)]));
        Assert.Contains(entity.GetIndexes(), x =>
            x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([
                nameof(CustomerAggregate.CitizenDocumentType),
                nameof(CustomerAggregate.CitizenIssuingCountryCode),
                nameof(CustomerAggregate.CitizenDocumentNumber),
            ]));
        Assert.True(entity.FindProperty(nameof(CustomerAggregate.Version))!.IsConcurrencyToken);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType createReference =
            dbContext.Model.FindEntityType(typeof(CustomerCreateReference))!;
        Assert.Contains(createReference.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CustomerCreateReference.RefId)]));

        Microsoft.EntityFrameworkCore.Metadata.IEntityType accountLinkage =
            dbContext.Model.FindEntityType(typeof(CustomerAccountLinkage))!;
        Assert.Contains(accountLinkage.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CustomerAccountLinkage.AccountId)]));
    }

    [Fact]
    public void CustomerCreateReference_ValidatesRefId_contract()
    {
        Assert.Equal("partner:customer-001", CustomerCreateReference.NormalizeRefId(" partner:customer-001 "));
        Assert.Throws<ArgumentException>(() => CustomerCreateReference.NormalizeRefId("bad ref id"));
        Assert.Throws<ArgumentException>(() => CustomerCreateReference.NormalizeRefId(new string('a', 101)));
    }

    [Fact]
    public void CustomerAccountLinkage_Requires_reason_and_preserves_history_on_unlink()
    {
        CustomerAggregate customer = CreateCustomer();
        CustomerAccountLinkage linkage = CustomerAccountLinkage.Link(
            customer,
            "account-1234",
            "operator-1",
            "onboarding",
            Now
        );

        linkage.Unlink("operator-2", "login account closed", Now.AddMinutes(1));

        Assert.False(linkage.IsActive);
        Assert.Equal("operator-2", linkage.UnlinkedByActorUserId);
        Assert.Throws<InvalidOperationException>(() =>
            linkage.Unlink("operator-3", "again", Now.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => CustomerAccountLinkage.Link(
            customer,
            "account-2",
            "operator-1",
            " ",
            Now
        ));
    }

    private static CustomerAggregate CreateCustomer()
    {
        CitizenDocument document = CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId,
            "VN",
            "001234567890"
        );
        return CustomerAggregate.Create("CUS_0123456789ABCDEFG", "Test Customer", null, document, Now);
    }

}
