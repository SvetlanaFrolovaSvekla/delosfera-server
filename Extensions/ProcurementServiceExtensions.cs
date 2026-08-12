using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Extensions;

public static class ProcurementServiceExtensions
{
    /// <summary>Контур закупок (раздел 6 ТЗ): матрица полномочий, заявки, процедуры.</summary>
    public static WebApplicationBuilder AddProcurementServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAuthorityMatrixService, AuthorityMatrixService>();
        builder.Services.AddScoped<IProcurementRequestService, ProcurementRequestService>();
        builder.Services.AddScoped<IProposalService, ProposalService>();
        builder.Services.AddScoped<IProtocolService, ProtocolService>();
        builder.Services.AddScoped<IProcurementRouteService, ProcurementRouteService>();
        builder.Services.AddScoped<ITenderService, TenderService>();
        builder.Services.AddScoped<ISupplierService, SupplierService>();
        builder.Services.AddScoped<IContractService, ContractService>();
        builder.Services.AddScoped<IPlanService, PlanService>();
        builder.Services.AddScoped<IGuaranteeService, GuaranteeService>();
        builder.Services.AddScoped<IClaimService, ClaimService>();
        builder.Services.AddScoped<IPublicationService, PublicationService>();
        return builder;
    }
}
