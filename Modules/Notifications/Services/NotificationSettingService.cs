using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Notifications.Models;

namespace delosfera_server.Modules.Notifications.Services;

/// <summary>Настройки уведомлений пользователя (УВ-16).</summary>
public class NotificationSettingDto
{
    public bool EmailDigestEnabled { get; set; } = true;
}

public interface INotificationSettingService
{
    Task<NotificationSettingDto> GetAsync(int userId);
    Task<NotificationSettingDto> SetAsync(int userId, NotificationSettingDto dto);

    /// <summary>Кто из списка отключил email-дайджест — их пропускает рассылка (УВ-15).</summary>
    Task<HashSet<int>> DigestOptOutAsync(IEnumerable<int> userIds);
}

/// <summary>
/// Персональные настройки уведомлений (УВ-16). Запись создаётся при первом изменении;
/// пока её нет, действуют значения по умолчанию (всё включено).
/// </summary>
public class NotificationSettingService : INotificationSettingService
{
    private readonly DelosferaDbContext _db;

    public NotificationSettingService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationSettingDto> GetAsync(int userId)
    {
        var s = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        return new NotificationSettingDto {EmailDigestEnabled = s?.EmailDigestEnabled ?? true};
    }

    public async Task<NotificationSettingDto> SetAsync(int userId, NotificationSettingDto dto)
    {
        var s = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (s is null)
        {
            s = new NotificationSetting {UserId = userId};
            _db.NotificationSettings.Add(s);
        }

        s.EmailDigestEnabled = dto.EmailDigestEnabled;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new NotificationSettingDto {EmailDigestEnabled = s.EmailDigestEnabled};
    }

    public async Task<HashSet<int>> DigestOptOutAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var optOut = await _db.NotificationSettings
            .Where(s => ids.Contains(s.UserId) && !s.EmailDigestEnabled)
            .Select(s => s.UserId)
            .ToListAsync();
        return optOut.ToHashSet();
    }
}
