using Friday.BuildingBlocks.Application;
using Friday.Modules.Admin.Application;
using Friday.Modules.Customer.Application;
using Friday.Modules.PaymentLedger.Application;
using LinKit.Core.Cqrs;

namespace Friday.API.Cqrs;

[CqrsContext(
    typeof(AdminApplicationAssemblyMarker),
    typeof(CustomerApplicationAssemblyMarker),
    typeof(PaymentLedgerApplicationAssemblyMarker),
    typeof(BuildingBlockApplicationMarker)
)]
public class CqrsContext { }
