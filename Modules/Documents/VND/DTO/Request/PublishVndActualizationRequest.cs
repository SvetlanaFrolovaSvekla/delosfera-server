namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class PublishVndActualizationRequest
{
    /// <summary>Прошла ли актуализация с изменениями</summary>
    public required bool HadChanges { get; set; }

    /// <summary>Обязательно, если у ВНД Period == Custom и был выбран сдвиг периода</summary>
    public DateOnly? NewDueActualizationDate { get; set; }
}