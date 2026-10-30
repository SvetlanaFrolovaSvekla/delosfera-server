using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IStatementRubricService
{
    Task<List<StatementRubricResponse>> GetAllAsync(StatementRubricSortBy sortBy, string? search, string languageCode);
    Task<StatementRubricResponse> CreateAsync(CreateStatementRubricRequest request, string languageCode);
    Task<StatementRubricResponse> UpdateAsync(int id, UpdateStatementRubricRequest request, string languageCode);
    Task DeleteAsync(int id);
}
