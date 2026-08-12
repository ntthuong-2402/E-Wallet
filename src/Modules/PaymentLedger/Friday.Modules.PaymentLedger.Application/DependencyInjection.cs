using Friday.BuildingBlocks.Application.Authorization;
using Friday.Modules.PaymentLedger.Application.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.PaymentLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentLedgerApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionContribution, PaymentLedgerPermissionContribution>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
