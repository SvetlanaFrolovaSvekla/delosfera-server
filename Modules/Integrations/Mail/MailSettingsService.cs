using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Security;
using delosfera_server.Data;

namespace delosfera_server.Modules.Integrations.Mail;

public record MailSettingsDto(
    bool Enabled,
    string Host,
    int Port,
    bool UseSsl,
    string? User,
    /// <summary>Пароль наружу не отдаётся — только признак, что он задан.</summary>
    bool HasPassword,
    string FromAddress,
    string FromName,
    string BaseUrl,
    int MaxAttempts);

public record MailSettingsRequest(
    bool Enabled,
    string Host,
    int Port,
    bool UseSsl,
    string? User,
    /// <summary>Пусто — оставить прежний пароль.</summary>
    string? Password,
    string FromAddress,
    string FromName,
    string BaseUrl,
    int MaxAttempts);

public interface IMailSettingsService
{
    Task<MailSettings> LoadAsync(CancellationToken ct = default);
    Task<MailSettingsDto> GetAsync(CancellationToken ct = default);
    Task<MailSettingsDto> SaveAsync(MailSettingsRequest request, CancellationToken ct = default);
}

/// <summary>
/// Настройки почты: в базе, с конфигурацией сервера как первоначальным значением.
///
/// Тот же порядок, что и у службы каталогов: администратор правит адрес и
/// выключатель через интерфейс, не трогая сервер, а раздел Mail в конфигурации
/// нужен лишь для первого запуска на пустой базе.
/// </summary>
public class MailSettingsService(
    DelosferaDbContext db,
    ISecretProtector protector,
    IOptions<MailOptions> configured) : IMailSettingsService
{
    private readonly MailOptions _configured = configured.Value;

    public async Task<MailSettings> LoadAsync(CancellationToken ct = default)
    {
        var settings = await db.MailSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        // Первый запуск: переносим значения из конфигурации сервера, чтобы
        // работавшая рассылка не выключилась сама собой при обновлении.
        var now = DateTime.UtcNow;
        settings = new MailSettings
        {
            Enabled = _configured.Enabled,
            Host = _configured.Host,
            Port = _configured.Port,
            UseSsl = _configured.UseSsl,
            User = _configured.User,
            PasswordEncrypted = string.IsNullOrEmpty(_configured.Password)
                ? null
                : protector.Protect(_configured.Password),
            FromAddress = _configured.FromAddress,
            FromName = _configured.FromName,
            BaseUrl = _configured.BaseUrl,
            MaxAttempts = _configured.MaxAttempts,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.MailSettings.Add(settings);
        await db.SaveChangesAsync(ct);

        return settings;
    }

    public async Task<MailSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await LoadAsync(ct);

        return new MailSettingsDto(
            s.Enabled, s.Host, s.Port, s.UseSsl, s.User,
            !string.IsNullOrEmpty(s.PasswordEncrypted),
            s.FromAddress, s.FromName, s.BaseUrl, s.MaxAttempts);
    }

    public async Task<MailSettingsDto> SaveAsync(MailSettingsRequest request, CancellationToken ct = default)
    {
        if (request.Enabled && string.IsNullOrWhiteSpace(request.Host))
            throw new InvalidOperationException("Укажите адрес почтового сервера — без него письма отправлять некуда.");

        if (request.Port is < 1 or > 65535)
            throw new InvalidOperationException("Недопустимый номер порта.");

        if (request.Enabled && string.IsNullOrWhiteSpace(request.FromAddress))
            throw new InvalidOperationException("Укажите адрес отправителя.");

        var s = await LoadAsync(ct);

        s.Enabled = request.Enabled;
        s.Host = request.Host.Trim();
        s.Port = request.Port;
        s.UseSsl = request.UseSsl;
        s.User = string.IsNullOrWhiteSpace(request.User) ? null : request.User.Trim();
        s.FromAddress = request.FromAddress.Trim();
        s.FromName = request.FromName.Trim();
        s.BaseUrl = request.BaseUrl.Trim();

        // Меньше одной попытки означало бы «не отправлять вовсе» — для этого
        // есть выключатель, и путать два способа не стоит.
        s.MaxAttempts = Math.Clamp(request.MaxAttempts, 1, 20);

        // Пустой пароль означает «оставить прежний»: администратор правит адрес
        // или выключатель, а пароля у него под рукой может не быть.
        if (!string.IsNullOrWhiteSpace(request.Password))
            s.PasswordEncrypted = protector.Protect(request.Password.Trim());

        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetAsync(ct);
    }
}
