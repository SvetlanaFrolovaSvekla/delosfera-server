using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Extensions;

public static class DocumentServiceExtensions
{
    public static WebApplicationBuilder AddDocumentServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<INumeratorService, NumeratorService>();

        // Администрируемость: типы документов, их карточки и представления журналов
        // (GEN-06, GEN-10)
        builder.Services.AddScoped<IDocumentTypeDefinitionService, DocumentTypeDefinitionService>();
        builder.Services.AddScoped<ICustomDocumentService, CustomDocumentService>();
        builder.Services.AddScoped<IListViewService, ListViewService>();
        builder.Services.AddScoped<IDocumentService, DocumentService>();
        return builder;
    }
}
