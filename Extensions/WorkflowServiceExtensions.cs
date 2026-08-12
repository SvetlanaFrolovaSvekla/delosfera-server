using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Extensions;

public static class WorkflowServiceExtensions
{
    public static WebApplicationBuilder AddWorkflowServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IRouteEngine, RouteEngine>();

        // Адресные уведомления по задачам и итогам маршрута (GEN-12, SZ-03, PRC-23)
        builder.Services.AddScoped<IWorkflowNotifier, WorkflowNotifier>();
        builder.Services.AddScoped<ITaskInboxService, TaskInboxService>();
        builder.Services.AddHostedService<OverdueWorker>();
        return builder;
    }
}
