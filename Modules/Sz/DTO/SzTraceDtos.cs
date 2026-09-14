namespace delosfera_server.Modules.Sz.DTO;

/// <summary>
/// Веха пути записки (СЗ-8): вход в очередной статус — когда, кто перевёл и сколько
/// записка в этом статусе пробыла. Курированный путь по жизненному циклу, а не сырой
/// журнал аудита: показывает движение записки, а не каждое поле, которое кто-то правил.
/// </summary>
public class SzTraceStep
{
    public required string Status { get; set; }
    public required string StatusTitle { get; set; }

    /// <summary>Когда записка вошла в этот статус.</summary>
    public DateTime At { get; set; }

    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }

    /// <summary>Сколько часов записка пробыла в этом статусе; для текущего — по сей момент.</summary>
    public double? DurationHours { get; set; }

    /// <summary>Текущий статус записки — путь пока не завершён.</summary>
    public bool IsCurrent { get; set; }
}
