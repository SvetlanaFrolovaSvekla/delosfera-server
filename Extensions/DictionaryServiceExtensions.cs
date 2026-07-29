using delosfera_server.Modules.Dictionaries.Services;

namespace delosfera_server.Extensions;

public static class DictionaryServiceExtensions
{
    public static WebApplicationBuilder AddDictionaryServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ITypeVndService, TypeVndService>();
        builder.Services.AddScoped<ISecurityLevelService, SecurityLevelService>();
        builder.Services.AddScoped<IApprovalBodyService, ApprovalBodyService>();
        builder.Services.AddScoped<IOrganizationUnitService, OrganizationUnitService>();
        builder.Services.AddScoped<IKeywordService, KeywordService>();
        builder.Services.AddScoped<IPositionService, PositionService>();
        builder.Services.AddScoped<IUserGroupService, UserGroupService>();
        builder.Services.AddScoped<IRubricService, RubricService>();
        
        return builder;
    }
}