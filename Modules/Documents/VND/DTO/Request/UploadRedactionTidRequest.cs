namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Запрос кнопки "Сформировать или загрузить ТИД" - прикладывает файл ТИД (Таблица
/// изменений и дополнений) к уже загруженному черновику последней редакции. Введён вместе с
/// упрощением VndUploadRedactionModal: раньше ТИД был обязателен прямо при загрузке редакции
/// актуализации, теперь его можно приложить отдельным шагом до отправки на согласование (см.
/// VndService.UploadTidForLastRedactionAsync и проверки в VndApprovalService.StartAsync /
/// VndService.PublishRedactionWithoutApprovalAsync).</summary>
public class UploadRedactionTidRequest
{
    public required IFormFile Tid { get; set; }
}
