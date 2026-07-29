using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IPositionService
{
    Task<List<PositionResponse>> GetAllAsync(PositionSortBy sortBy, string? search, string languageCode);
    Task<PositionResponse> CreateAsync(CreatePositionRequest request, string languageCode);
    Task<PositionResponse> UpdateAsync(int id, UpdatePositionRequest request, string languageCode);
    Task DeleteAsync(int id);
}