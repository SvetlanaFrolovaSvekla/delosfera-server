using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IOrganizationUnitService
{
    Task<List<OrganizationUnitResponse>> GetAllAsync(OrganizationUnitSortBy sortBy, string? search, string languageCode);
    Task<OrganizationUnitResponse> CreateAsync(CreateOrganizationUnitRequest request, string languageCode);
    Task<OrganizationUnitResponse> UpdateAsync(int id, UpdateOrganizationUnitRequest request, string languageCode);

    /// <summary>История изменений подразделения (GEN-08).</summary>
    Task<List<OrgUnitHistoryResponse>> GetHistoryAsync(int id);

    /// <summary>
    /// Состав оргструктуры на дату: кто был начальником и куратором, как называлось
    /// подразделение. Нужен при разборе старых согласований (GEN-08).
    /// </summary>
    Task<List<OrgUnitSnapshotResponse>> GetSnapshotAsync(DateOnly date, string languageCode);
    Task DeleteAsync(int id);
}