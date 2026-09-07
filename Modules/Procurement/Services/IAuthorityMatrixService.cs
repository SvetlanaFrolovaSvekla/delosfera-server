using delosfera_server.Modules.Procurement.DTO;

namespace delosfera_server.Modules.Procurement.Services;

public interface IAuthorityMatrixService
{
    /// <summary>Подобрать способ закупки, состав согласования и орган утверждения по сумме (PRC-04).</summary>
    Task<MatrixResolveResponse> ResolveAsync(MatrixResolveRequest request);

    /// <summary>Матрица целиком — приложение №1 с пересчётом процентных порогов в сомы.</summary>
    Task<MatrixTableDto> GetTableAsync();

    // ── настройка ────────────────────────────────────────────────────────────

    /// <summary>Правила матрицы в сыром виде — для экрана настройки.</summary>
    Task<List<MatrixRuleEditDto>> RulesForEditAsync();

    /// <summary>Способы закупки для настройки: минимум КП и подписи.</summary>
    Task<List<ProcurementMethodEditDto>> MethodsForEditAsync();

    Task<MatrixRuleEditDto> CreateRuleAsync(MatrixRuleSaveRequest request, int actorUserId);
    Task<MatrixRuleEditDto> UpdateRuleAsync(int id, MatrixRuleSaveRequest request, int actorUserId);
    Task DeleteRuleAsync(int id, int actorUserId);

    Task<ProcurementMethodEditDto> UpdateMethodAsync(int id, ProcurementMethodSaveRequest request, int actorUserId);
}
