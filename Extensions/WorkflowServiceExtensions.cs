using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Extensions;

public static class WorkflowServiceExtensions
{
    public static WebApplicationBuilder AddWorkflowServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IRouteEngine, RouteEngine>();
        builder.Services.AddHostedService<OverdueWorker>();
        return builder;
    }
}
