using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IActualizationBucketSettingsService
{
    Task<ActualizationBucketSettingsResponse> GetAsync();
    Task<ActualizationBucketSettingsResponse> UpdateAsync(UpdateActualizationBucketSettingsRequest request);
}

/// <summary>
/// Справочник порогов индикации сроков актуализации ВНД (Normal/Approaching/Critical) —
/// раздел ВНД в Справочниках. Запись одна на всю систему (см. ActualizationBucketSettings).
///
/// При каждом чтении и сохранении обновляет ActualizationThresholds — кэш порогов в
/// памяти процесса, которым пользуется VndService при расчёте статуса срока по каждому
/// документу и в сводке по дашборду.
/// </summary>
public class ActualizationBucketSettingsService : IActualizationBucketSettingsService
{
    /// <summary>10 лет — разумный потолок, чтобы опечатка в поле не улетела в тысячи дней.</summary>
    private const int MaxDays = 3650;

    private readonly DelosferaDbContext _db;

    public ActualizationBucketSettingsService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<ActualizationBucketSettingsResponse> GetAsync()
    {
        var settings = await LoadAsync();
        ActualizationThresholds.Configure(settings.CriticalDays, settings.ApproachingDays);
        return ToResponse(settings);
    }

    public async Task<ActualizationBucketSettingsResponse> UpdateAsync(UpdateActualizationBucketSettingsRequest request)
    {
        if (request.CriticalDays < 0 || request.ApproachingDays < 0)
            throw new InvalidOperationException("Пороги задаются в днях и не бывают отрицательными");

        if (request.CriticalDays > MaxDays || request.ApproachingDays > MaxDays)
            throw new InvalidOperationException($"Порог не может превышать {MaxDays} дней");

        if (request.CriticalDays >= request.ApproachingDays)
            throw new InvalidOperationException(
                "Порог \"Критично\" должен быть меньше порога \"Приближается\"");

        var settings = await LoadAsync();
        settings.CriticalDays = request.CriticalDays;
        settings.ApproachingDays = request.ApproachingDays;

        await _db.SaveChangesAsync();

        ActualizationThresholds.Configure(settings.CriticalDays, settings.ApproachingDays);
        return ToResponse(settings);
    }

    private async Task<ActualizationBucketSettings> LoadAsync()
    {
        var settings = await _db.Set<ActualizationBucketSettings>().FirstOrDefaultAsync();
        if (settings is not null) return settings;

        // Значения по умолчанию засеяны миграцией; страховка на случай пустой таблицы.
        settings = new ActualizationBucketSettings();
        _db.Set<ActualizationBucketSettings>().Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private static ActualizationBucketSettingsResponse ToResponse(ActualizationBucketSettings s) => new()
    {
        CriticalDays = s.CriticalDays,
        ApproachingDays = s.ApproachingDays,
    };
}
