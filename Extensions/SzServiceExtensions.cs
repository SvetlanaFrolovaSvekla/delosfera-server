using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Extensions;

public static class SzServiceExtensions
{
    public static WebApplicationBuilder AddSzServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISzService, SzService>();
        builder.Services.AddScoped<ISzExecutionService, SzExecutionService>();
        builder.Services.AddScoped<ISzPaperService, SzPaperService>();
        builder.Services.AddScoped<ISzArchiveService, SzArchiveService>();
        builder.Services.AddScoped<ISzProcurementService, SzProcurementService>();
        builder.Services.AddScoped<IRouteCompletionHandler, SzRouteCompletionHandler>();
        return builder;
    }
}
