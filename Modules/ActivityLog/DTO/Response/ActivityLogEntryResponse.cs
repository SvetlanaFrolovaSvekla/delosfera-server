namespace delosfera_server.Modules.ActivityLog.DTO.Response;

public class ActivityLogEntryResponse
{
    public int Id { get; set; }
    public required string Module { get; set; }
    public int EntityId { get; set; }
    public required string EntityCode { get; set; }

    /// <summary>"check" | "x" | "doc" | "clock" | "info" - иконка типа действия</summary>
    public required string Icon { get; set; }

    /// <summary>Точный тип события из журнала ВНД (ActivityEventKind в camelCase: "created",
    /// "itemAdded", "processStarted", "approved", "revisionNeeded", "published"...) — Icon выше
    /// сворачивает их в несколько групп для дашборда, а вкладке «История» ВНД нужен свой цвет
    /// на каждый тип (см. AuditLogPanel на фронте). Null — запись выведена из технического
    /// аудита (СЗ/закупки), у неё есть только Icon.</summary>
    public string? Kind { get; set; }

    public required string Text { get; set; }
    public required string Url { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>false — запись о чужом черновике ВНД, который текущий пользователь не вправе
    /// открыть (см. тот же критерий видимости черновика, что и в VndService.GetByIdAsync).
    /// Раньше это никак не отражалось на виджете "Последняя активность"/странице "Активность
    /// на портале": строка выглядела как обычная кликабельная, а переход по её ссылке падал с
    /// ошибкой доступа. true — по умолчанию и для всех записей не о черновике ВНД (сам ВНД уже
    /// не черновик, либо это модуль СЗ/закупок — там такого ограничения на просмотр нет).</summary>
    public bool CanOpen { get; set; } = true;
}