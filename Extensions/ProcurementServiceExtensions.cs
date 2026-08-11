using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Extensions;

public static class ProcurementServiceExtensions
{
    /// <summary>Контур закупок (раздел 6 ТЗ): матрица полномочий, заявки, процедуры.</summary>
    public static WebApplicationBuilder AddProcurementServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAuthorityMatrixService, AuthorityMatrixService>();
        builder.Services.AddScoped<IProcurementRequestService, ProcurementRequestService>();
        return builder;
    }
}
