namespace delosfera_server.Modules.Files.Services;

/// <summary>
/// Проверяет, имеет ли текущий пользователь право получить конкретный файл.
/// Реализация живёт в доменном модуле (VND), т.к. правила доступа определяются
/// документом, к которому привязан файл. Интерфейс объявлен здесь, чтобы контроллер
/// файлов не зависел от доменных сервисов напрямую.
/// </summary>
public interface IFileAccessAuthorizer
{
    Task<bool> CanCurrentUserAccessAsync(int fileId, CancellationToken ct = default);

    /// <summary>
    /// Можно ли текущему пользователю именно СКАЧАТЬ файл (а не только просмотреть его в браузере).
    /// Проверяется дополнительно к <see cref="CanCurrentUserAccessAsync"/>, когда клиент явно
    /// запрашивает скачивание (?download=true). По умолчанию — без ограничений.
    /// </summary>
    Task<bool> CanCurrentUserExportAsync(int fileId, CancellationToken ct = default) => Task.FromResult(true);
}
