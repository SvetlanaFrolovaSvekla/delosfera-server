using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface ITypeVndService
{
    Task<List<TypeVndResponse>> GetAllAsync(TypeVndSortBy sortBy, string? search, string languageCode);
    Task<TypeVndResponse> CreateAsync(CreateTypeVndRequest request, string languageCode);
    Task<TypeVndResponse> UpdateAsync(int id, UpdateTypeVndRequest request, string languageCode);
    Task DeleteAsync(int id);
}