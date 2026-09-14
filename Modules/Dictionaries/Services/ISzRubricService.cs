using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface ISzRubricService
{
    Task<List<SzRubricResponse>> GetAllAsync(SzRubricSortBy sortBy, string? search, string languageCode);
    Task<SzRubricResponse> CreateAsync(CreateSzRubricRequest request, string languageCode);
    Task<SzRubricResponse> UpdateAsync(int id, UpdateSzRubricRequest request, string languageCode);
    Task DeleteAsync(int id);
}
