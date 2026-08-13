using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.ActivityLog.Models;

/// <summary>Запись единого журнала активности по всем модулям документов.</summary>
public class ActivityLogEntry : IAuditableEntity
{
    public int Id { get; set; }

    public required string Module { get; set; } // vnd, sz (к какому модулю относится)

    // ID документа
    public int EntityId { get; set; }  

    /// Код документа на момент события
    public required string EntityCode { get; set; }

    public ActivityEventKind Kind { get; set; }

    // Кто совершил действие
    public int? ActorUserId { get; set; } // null - системное событие (автоакцепт по таймауту)
    public User? ActorUser { get; set; }

    public required string TextRu { get; set; }
    public string? TextEn { get; set; }
    public string? TextKg { get; set; }

    // Ссылка на карточку документа
    public required string Url { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}