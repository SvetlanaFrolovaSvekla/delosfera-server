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
}
