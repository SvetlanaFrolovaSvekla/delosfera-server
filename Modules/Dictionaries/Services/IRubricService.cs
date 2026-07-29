using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IRubricService
{
    Task<List<RubricResponse>> GetAllAsync(RubricSortBy sortBy, string? search, string languageCode);
    Task<RubricResponse> CreateAsync(CreateRubricRequest request, string languageCode);
    Task<RubricResponse> UpdateAsync(int id, UpdateRubricRequest request, string languageCode);
    Task DeleteAsync(int id);
}