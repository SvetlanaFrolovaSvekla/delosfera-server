using delosfera_server.Modules.Documents.VND.Services;

namespace delosfera_server.Extensions;

public static class VndServiceExtensions
{
    public static WebApplicationBuilder AddVndServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndService, VndService>();
        return builder;
    }
}