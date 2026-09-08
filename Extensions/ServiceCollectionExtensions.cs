using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using delosfera_server.Data;

namespace delosfera_server.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DelosferaDbContext>(options =>
            options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    // Реестры и карточки тянут по нескольку коллекций разом (.Include). В одном
                    // JOIN это давало декартово произведение строк-потомков и лишнюю работу; сплит
                    // по умолчанию разбивает такой запрос на отдельные к каждой коллекции. Действует
                    // только там, где коллекции есть — запросы с одними ссылочными Include не меняются.
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        return builder;
    }
}