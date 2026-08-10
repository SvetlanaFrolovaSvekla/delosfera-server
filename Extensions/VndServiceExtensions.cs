using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Extensions;

public static class VndServiceExtensions
{
    public static WebApplicationBuilder AddVndServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndService, VndService>();
        builder.Services.AddScoped<IFileAccessAuthorizer, VndFileAccessAuthorizer>();
        return builder;
    }
}