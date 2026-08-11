using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Extensions;

public static class DocumentServiceExtensions
{
    public static WebApplicationBuilder AddDocumentServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<INumeratorService, NumeratorService>();
        builder.Services.AddScoped<IDocumentService, DocumentService>();
        return builder;
    }
}
