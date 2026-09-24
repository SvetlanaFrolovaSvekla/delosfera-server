using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndApprovalNormSettingsService
{
    Task<VndApprovalNormSettingsResponse> GetAsync();
    Task<VndApprovalNormSettingsResponse> UpdateAsync(UpdateVndApprovalNormSettingsRequest request);
}

/// <summary>
/// Справочник нормативов согласования редакции ВНД по умолчанию — раздел ВНД в
/// Справочниках. Запись одна на всю систему (см. VndApprovalNormSettings).
/// </summary>
public class VndApprovalNormSettingsService : IVndApprovalNormSettingsService
{
    private readonly DelosferaDbContext _db;
    private readonly IVndWorkCalendarCache _workCalendar;

    public VndApprovalNormSettingsService(DelosferaDbContext db, IVndWorkCalendarCache workCalendar)
    {
        _db = db;
        _workCalendar = workCalendar;
    }

    public async Task<VndApprovalNormSettingsResponse> GetAsync()
        => ToResponse(await LoadAsync());

    public async Task<VndApprovalNormSettingsResponse> UpdateAsync(UpdateVndApprovalNormSettingsRequest request)
    {
        // 90 рабочих дней по текущему рабочему времени банка (справочник "Производственный календарь")
        var max = (await _workCalendar.GetRulesAsync()).MaxDeadlineMinutes;
        Validate(request.PrimaryDeadlineMinutes, "Первичное согласование", max);
        Validate(request.RepeatDeadlineMinutes, "Согласование после внесённых изменений", max);
        Validate(request.FinalHoldDeadlineMinutes, "Финальная выдержка", max);

        var settings = await LoadAsync();
        settings.PrimaryDeadlineMinutes = request.PrimaryDeadlineMinutes;
        settings.RepeatDeadlineMinutes = request.RepeatDeadlineMinutes;
        settings.FinalHoldDeadlineMinutes = request.FinalHoldDeadlineMinutes;

        await _db.SaveChangesAsync();
        return ToResponse(settings);
    }

    private static void Validate(int minutes, string stage, int maxDeadlineMinutes)
    {
        if (minutes <= 0)
            throw new InvalidOperationException($"Норматив «{stage}» должен быть больше нуля");
        if (minutes > maxDeadlineMinutes)
            throw new InvalidOperationException($"Норматив «{stage}» не может превышать 90 рабочих дней");
    }

    private async Task<VndApprovalNormSettings> LoadAsync()
    {
        var settings = await _db.Set<VndApprovalNormSettings>().FirstOrDefaultAsync();
        if (settings is not null) return settings;

        // Значения по умолчанию засеяны миграцией; страховка на случай пустой таблицы.
        settings = new VndApprovalNormSettings();
        _db.Set<VndApprovalNormSettings>().Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private static VndApprovalNormSettingsResponse ToResponse(VndApprovalNormSettings s) => new()
    {
        PrimaryDeadlineMinutes = s.PrimaryDeadlineMinutes,
        RepeatDeadlineMinutes = s.RepeatDeadlineMinutes,
        FinalHoldDeadlineMinutes = s.FinalHoldDeadlineMinutes,
    };
}
