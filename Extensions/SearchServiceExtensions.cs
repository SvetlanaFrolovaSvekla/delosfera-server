using delosfera_server.Modules.Search.Services;

namespace delosfera_server.Extensions;

public static class SearchServiceExtensions
{
    /// <summary>Поиск по документам и сохранённые фильтры (GEN-02, GEN-04).</summary>
    public static WebApplicationBuilder AddSearchServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISearchService, SearchService>();
        builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
        return builder;
    }
}
