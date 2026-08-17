using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Common.Services.Authorization.Ldap;
using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Integrations.Controllers;

/// <summary>
/// Системные настройки: связь со службой каталогов.
///
/// Раздел для администратора системы, а не для администратора сервера: адрес
/// каталога, учётная запись и частота синхронизации меняются через интерфейс и
/// вступают в силу без перезапуска.
/// </summary>
[ApiController]
[Route("api/system/directory")]
[Tags("Системные настройки")]
[Authorize]
public class SystemSettingsController : ControllerBase
{
    private readonly IDirectorySettingsService _settings;
    private readonly ILdapDirectory _directory;
    private readonly IDirectorySyncService _sync;
    private readonly ICurrentUserService _currentUser;

    public SystemSettingsController(
        IDirectorySettingsService settings,
        ILdapDirectory directory,
        IDirectorySyncService sync,
        ICurrentUserService currentUser)
    {
        _settings = settings;
        _directory = directory;
        _sync = sync;
        _currentUser = currentUser;
    }

    /// <summary>Текущие настройки связи со службой каталогов. Пароль не возвращается.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _settings.GetAsync(ct));

    /// <summary>Сохранить настройки. Пустое поле пароля оставляет прежний.</summary>
    [HttpPut]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update([FromBody] DirectorySettingsRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _settings.UpdateAsync(request, _currentUser.UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }

    /// <summary>
    /// Проверить связь: система подключается к каталогу и считает, сколько
    /// пользователей видит. Без этого настройки проверялись бы только тем, что
    /// через час чего-то не произошло.
    /// </summary>
    [HttpPost("test")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        try
        {
            var users = await _directory.ListUsersAsync(ct);

            return Ok(new
            {
                success = true,
                total = users.Count,
                active = users.Count(u => !u.IsDisabled),
                message = $"Связь установлена. В каталоге найдено {users.Count} пользователей.",
            });
        }
        catch (Exception ex)
        {
            // Ошибку показываем как есть: администратору нужно понять, что именно
            // не так — адрес, учётная запись, сертификат или ветка поиска.
            return Ok(new {success = false, total = 0, active = 0, message = ex.Message});
        }
    }

    /// <summary>Синхронизировать сейчас, не дожидаясь расписания.</summary>
    [HttpPost("sync")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> SyncNow(CancellationToken ct)
    {
        try
        {
            var result = await _sync.SyncAsync(_currentUser.UserId, ct);
            await _settings.RecordSyncAsync(
                result.Created.Count, result.Updated.Count, result.Deactivated.Count, null, ct);

            // Пропущенные записи называем числом и первыми примерами: молчаливый
            // пропуск сотрудника выглядел бы как успешная синхронизация.
            var пропущено = result.Skipped.Count == 0
                ? string.Empty
                : $" Пропущено: {result.Skipped.Count} — {string.Join("; ", result.Skipped.Take(3))}"
                  + (result.Skipped.Count > 3 ? " и другие." : ".");

            return Ok(new
            {
                success = true,
                created = result.Created.Count,
                updated = result.Updated.Count,
                deactivated = result.Deactivated.Count,
                skipped = result.Skipped.Count,
                message = $"Готово: создано {result.Created.Count}, обновлено {result.Updated.Count}, "
                          + $"деактивировано {result.Deactivated.Count}.{пропущено}",
            });
        }
        catch (Exception ex)
        {
            await _settings.RecordSyncAsync(0, 0, 0, ex.Message, ct);
            return Ok(new {success = false, message = ex.Message});
        }
    }
}
