using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IOrganizationUnitService
{
    Task<List<OrganizationUnitResponse>> GetAllAsync(OrganizationUnitSortBy sortBy, string? search, string languageCode);
    Task<OrganizationUnitResponse> CreateAsync(CreateOrganizationUnitRequest request, string languageCode);
    Task<OrganizationUnitResponse> UpdateAsync(int id, UpdateOrganizationUnitRequest request, string languageCode);
    Task DeleteAsync(int id);
}