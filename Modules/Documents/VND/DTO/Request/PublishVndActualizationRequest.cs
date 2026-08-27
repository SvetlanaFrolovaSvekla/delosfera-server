namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class PublishVndActualizationRequest
{
    /// <summary>Прошла ли актуализация с изменениями</summary>
    public required bool HadChanges { get; set; }

    /// <summary>Обязательно, если у ВНД Period == Custom и был выбран сдвиг периода</summary>
    public DateOnly? NewDueActualizationDate { get; set; }

    // --- Реквизиты, обязательные к обновлению прямо в момент консолидации (см.
    // VndActualizationService.PublishAsync) - № принятия, дата принятия, дата вступления в
    // силу. Раньше эти поля правились только на вкладке "Реквизиты" и могли остаться
    // незаполненными/устаревшими к моменту публикации редакции.

    /// <summary>№ принятия (реквизит документа)</summary>
    public required string AdoptionCode { get; set; }

    /// <summary>Дата принятия (реквизит документа)</summary>
    public required DateOnly AdoptionDate { get; set; }

    /// <summary>Дата вступления в силу (реквизит документа)</summary>
    public required DateOnly EffectiveDate { get; set; }
}