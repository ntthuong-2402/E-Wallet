using Friday.BuildingBlocks.Application;
using Friday.Modules.Admin.Application;
using LinKit.Core.Cqrs;

namespace Friday.API.Cqrs;

[CqrsContext(typeof(AdminApplicationAssemblyMarker), typeof(BuildingBlockApplicationMarker))]
public class CqrsContext { }
