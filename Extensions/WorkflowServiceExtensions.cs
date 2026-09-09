using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Extensions;

public static class WorkflowServiceExtensions
{
    public static WebApplicationBuilder AddWorkflowServices(this WebApplicationBuilder builder)
    {
        // Роли шаблона превращаются в людей на запуске маршрута: шаблон должен
        // переживать смену людей в должностях.
        builder.Services.AddScoped<IRouteRoleResolver, RouteRoleResolver>();
        builder.Services.AddScoped<IRouteEngine, RouteEngine>();

        // Выбор шаблона маршрута по (тип документа + подразделение) — единый конструктор
        // согласующих взамен захардкоженных цепочек.
        builder.Services.AddScoped<IRouteTemplateSelector, RouteTemplateSelector>();

        // Адресные уведомления по задачам и итогам маршрута (GEN-12, SZ-03, PRC-23)
        builder.Services.AddScoped<IWorkflowNotifier, WorkflowNotifier>();
        builder.Services.AddScoped<ITaskInboxService, TaskInboxService>();
        builder.Services.AddHostedService<OverdueWorker>();
        return builder;
    }
}
