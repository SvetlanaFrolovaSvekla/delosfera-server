using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndService
{
    Task<List<VndResponse>> SearchAsync(VndSearchRequest request, string languageCode);
    Task<VndResponse> GetByIdAsync(int id, string languageCode);
    Task<VndResponse> CreateAsync(CreateVndRequest request, int currentUserId, string languageCode);
    Task DeleteAsync(int id, int currentUserId);
    /// <summary>Архивировать (отменить) ВНД — кнопка "Архивировать" (см. PermissionCode.CancelVnd).
    /// Недоступно для черновика (его можно только удалить) и для уже архивированного документа.
    /// Если документ на согласовании — согласование отзывается автоматически в рамках той же
    /// операции.</summary>
    Task<VndResponse> CancelAsync(int id, CancelVndRequest request, int currentUserId, string languageCode);
    Task<VndRedactionResponse> AddRedactionAsync(int vndId, CreateVndRedactionRequest request, int currentUserId);
    Task<List<VndRedactionResponse>> GetRedactionsAsync(int vndId);
    Task<VndRedactionResponse> PublishRedactionWithoutApprovalAsync(int vndId, int redactionId, int currentUserId);
    Task<VndActualizationSummaryResponse> GetActualizationSummaryAsync();
    /// <summary>Экспорт таблицы "Планирование актуализации" в Excel (кнопка "Экспорт плана в
    /// Excel") — та же фильтрация, что и в SearchAsync (request.Filter), плюс набор колонок,
    /// отмеченных пользователем в модалке экспорта (request.Columns). Обязательные (fixed на
    /// фронте) колонки экспортируются всегда, вне зависимости от их наличия в request.Columns —
    /// см. VndService.ExportActualizationPlanAsync.</summary>
    Task<byte[]> ExportActualizationPlanAsync(VndActualizationExportRequest request, string languageCode);
    Task<VndResponse> UpdateRequisitesAsync(int id, UpdateVndRequisitesRequest request, string languageCode);
    Task<VndLinksResponse> GetLinksAsync(int vndId, string languageCode);
    Task<VndLinkResponse> AddLinkAsync(int vndId, AddVndLinkRequest request, string languageCode);
    Task DeleteLinkAsync(int vndId, int linkId);
    /// <summary>Разрешает легаси-ссылку (db://documents/{code} или db://attachments/{n}) из
    /// текста документа vndId в реальный документ/вложение системы — см.
    /// LegacyLinkResolveResponse и DocxLegacyLinkExtractor. Бросает KeyNotFoundException, если
    /// ссылка ни на что не разрешилась.</summary>
    Task<LegacyLinkResolveResponse> ResolveLegacyLinkAsync(int vndId, string type, string legacyId);
    Task<VndRedactionResponse> EditLastRevisionDirectlyAsync(
        int vndId, EditLastRevisionDirectlyRequest request, int currentUserId);
    /// <summary>То же самое, что EditLastRevisionDirectlyAsync, но для ЛЮБОЙ редакции документа,
    /// а не только последней - главный редактор может править файлы и специальные вложения
    /// (ТИД/Лист согласования/Матрица разногласий) исторических редакций (например, у
    /// мигрированных из isrib документов, где этих файлов изначально нет).</summary>
    Task<VndRedactionResponse> EditRedactionDirectlyAsync(
        int vndId, int redactionId, EditLastRevisionDirectlyRequest request, int currentUserId);
    /// <summary>Приложить ТИД к последней редакции (кнопка "Сформировать или загрузить ТИД") —
    /// см. UploadRedactionTidRequest</summary>
    Task<VndRedactionResponse> UploadTidForLastRedactionAsync(
        int vndId, UploadRedactionTidRequest request, int currentUserId);
    Task<List<VndQuickSearchResponse>> QuickSearchAsync(string query, string languageCode, int limit);
}
