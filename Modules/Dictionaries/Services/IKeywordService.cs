using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IKeywordService
{
    Task<List<KeywordResponse>> GetAllAsync(KeywordSortBy sortBy, string? search, string languageCode);
    Task<KeywordResponse> CreateAsync(CreateKeywordRequest request, string languageCode);
    Task<KeywordResponse> UpdateAsync(int id, UpdateKeywordRequest request, string languageCode);
    Task DeleteAsync(int id);
}