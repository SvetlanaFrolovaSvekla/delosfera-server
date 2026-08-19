using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Замещение сотрудника на период отсутствия (GEN-14). Задачи согласования и подписания
/// перенаправляются замещающему с сохранением сроков, а в истории остаётся, кто именно
/// принял решение и по чьему замещению.
///
/// Период хранится датами, а не признаком «активно»: замещение оформляется заранее
/// и должно включаться и выключаться само, без ручного переключения в день выхода.
/// </summary>
public class Substitution : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Кого замещают — отсутствующий сотрудник.</summary>
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Кто замещает — принимает задачи на период.</summary>
    public int SubstituteUserId { get; set; }
    public User? SubstituteUser { get; set; }

    public DateOnly StartsOn { get; set; }

    /// <summary>Последний день замещения включительно.</summary>
    public DateOnly EndsOn { get; set; }

    /// <summary>Основание: отпуск, командировка, больничный.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Замещение отменено досрочно. Запись не удаляется: по ней уже могли пройти
    /// решения, и журнал должен объяснять, почему их принял другой сотрудник.
    /// </summary>
    public bool IsCancelled { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
