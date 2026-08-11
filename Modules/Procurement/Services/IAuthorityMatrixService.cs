using delosfera_server.Modules.Procurement.DTO;

namespace delosfera_server.Modules.Procurement.Services;

public interface IAuthorityMatrixService
{
    /// <summary>Подобрать способ закупки, состав согласования и орган утверждения по сумме (PRC-04).</summary>
    Task<MatrixResolveResponse> ResolveAsync(MatrixResolveRequest request);

    /// <summary>Матрица целиком — приложение №1 с пересчётом процентных порогов в сомы.</summary>
    Task<MatrixTableDto> GetTableAsync();
}
