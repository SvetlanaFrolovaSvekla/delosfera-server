using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Integrations.OrgStructure;

/// <summary>Чем закончился проход синхронизации.</summary>
public enum OrgSyncOutcome
{
    /// <summary>Прошла целиком.</summary>
    Success = 1,

    /// <summary>Прошла, но часть данных не легла — например, не нашлись сотрудники.</summary>
    Partial = 2,

    /// <summary>Не прошла: портал недоступен, токен отозван, ответ не разобрался.</summary>
    Failed = 3,
}

/// <summary>
/// Запись о проходе синхронизации оргструктуры.
///
/// История нужна не для полноты, а для одного вопроса: почему структура
/// выглядит не так, как в портале. Ответ — либо «последний проход упал три дня
/// назад», либо «прошёл, но сорок человек не сопоставились». Без истории это
/// выясняют, повторяя синхронизацию вручную и глядя, что изменится.
/// </summary>
public class OrgSyncRun
{
    public long Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public OrgSyncOutcome Outcome { get; set; }

    /// <summary>Кто запустил. Пусто — сработало расписание.</summary>
    public int? StartedByUserId { get; set; }

    // ── Подразделения ──

    public int UnitsReceived { get; set; }
    public int UnitsCreated { get; set; }
    public int UnitsUpdated { get; set; }

    /// <summary>
    /// Пришли из портала, но заводить их запрещено настройкой. Не ошибка —
    /// решение администратора, но знать о них он должен.
    /// </summary>
    public int UnitsSkipped { get; set; }

    // ── Сотрудники ──

    public int EmployeesReceived { get; set; }

    /// <summary>Сопоставлены с учётными записями системы.</summary>
    public int EmployeesMatched { get; set; }

    /// <summary>
    /// Пришли из портала, но такой учётной записи в системе нет. Обычное дело:
    /// в портале весь банк, а в системе — те, кому выдали доступ.
    /// </summary>
    public int EmployeesUnmatched { get; set; }

    /// <summary>У скольких сменилось подразделение, должность или руководитель.</summary>
    public int EmployeesUpdated { get; set; }

    /// <summary>
    /// Скольким закрыли доступ: в портале уволены. Считается отдельно от прочих
    /// правок — это единственное изменение, после которого человек не сможет
    /// войти, и администратор должен видеть его числом, а не искать в общей сумме.
    /// </summary>
    public int EmployeesDeactivated { get; set; }

    /// <summary>Ошибка, если проход не прошёл. Текст для администратора, не трассировка.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Замечания по ходу прохода: подразделения без места в структуре,
    /// сотрудники без почты и тому подобное. Хранятся строками, по одной на замечание.
    /// </summary>
    public string? NotesJson { get; set; }
}

public class OrgSyncRunConfiguration : IEntityTypeConfiguration<OrgSyncRun>
{
    public void Configure(EntityTypeBuilder<OrgSyncRun> b)
    {
        b.ToTable("org_sync_run");

        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.NotesJson).HasColumnType("jsonb");

        // История читается только сверху вниз: последние проходы за период.
        b.HasIndex(x => x.StartedAt).IsDescending();
    }
}
