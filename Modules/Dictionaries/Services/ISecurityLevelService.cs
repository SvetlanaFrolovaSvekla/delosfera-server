using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface ISecurityLevelService
{
    Task<List<SecurityLevelResponse>> GetAllAsync(SecurityLevelSortBy sortBy, string? search, string languageCode);
    Task<SecurityLevelResponse> CreateAsync(CreateSecurityLevelRequest request, string languageCode);
    Task<SecurityLevelResponse> UpdateAsync(int id, UpdateSecurityLevelRequest request, string languageCode);
    Task DeleteAsync(int id);
}