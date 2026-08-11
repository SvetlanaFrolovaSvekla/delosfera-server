using delosfera_server.Common.Extensions;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.DTO.Response;
using Microsoft.EntityFrameworkCore;

namespace delosfera_server.Modules.Users.Services;

/// <summary>
/// Лента активности пользователя. Отдельной таблицы аудита пока нет, поэтому активность
/// выводится из существующих данных ВНД: созданные документы, принятые решения по
/// согласованию и инициированные согласования.
/// </summary>
public class UserActivityService : IUserActivityService
{
    private readonly DelosferaDbContext _db;

    public UserActivityService(DelosferaDbContext db) => _db = db;

    public async Task<UserActivityResponse> GetActivityAsync(int userId, string languageCode, int recentLimit = 20)
    {
        var vndCreatedCount = await _db.VndDocuments.CountAsync(v => v.CreatedByUserId == userId);
        var approvalsInitiatedCount = await _db.VndApprovalProcesses.CountAsync(p => p.InitiatorUserId == userId);
        var approvalsDecidedCount = await _db.VndApprovalStages.CountAsync(s =>
            s.ApproverUserId == userId &&
            (s.PrimaryDecidedAt != null || s.RepeatDecidedAt != null || s.FinalHoldDecidedAt != null));

        var createdVnds = await _db.VndDocuments
            .Where(v => v.CreatedByUserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .Take(recentLimit)
            .Select(v => new { v.Id, v.Code, v.TitleRu, v.TitleEn, v.TitleKg, v.CreatedAt })
            .ToListAsync();

        var decidedStages = await _db.VndApprovalStages
            .Where(s => s.ApproverUserId == userId &&
                        (s.PrimaryDecidedAt != null || s.RepeatDecidedAt != null || s.FinalHoldDecidedAt != null))
            .Include(s => s.ApprovalProcess!).ThenInclude(p => p.Vnd)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(recentLimit)
            .ToListAsync();

        var initiatedProcesses = await _db.VndApprovalProcesses
            .Where(p => p.InitiatorUserId == userId)
            .Include(p => p.Vnd)
            .OrderByDescending(p => p.PrimaryStartedAt)
            .Take(recentLimit)
            .ToListAsync();

        var items = new List<UserActivityItemResponse>();

        items.AddRange(createdVnds.Select(v => new UserActivityItemResponse
        {
            Type = "vnd_created",
            VndId = v.Id,
            VndCode = v.Code,
            VndTitle = Resolve(v.TitleRu, v.TitleEn, v.TitleKg, languageCode),
            Timestamp = v.CreatedAt,
            Description = "Создан документ"
        }));

        items.AddRange(decidedStages.Select(s => new UserActivityItemResponse
        {
            Type = "approval_decided",
            VndId = s.ApprovalProcess?.Vnd?.Id,
            VndCode = s.ApprovalProcess?.Vnd?.Code,
            VndTitle = s.ApprovalProcess?.Vnd is { } vnd
                ? Resolve(vnd.TitleRu, vnd.TitleEn, vnd.TitleKg, languageCode) : null,
            Timestamp = LatestDecision(s),
            Description = DescribeDecision(s)
        }));

        items.AddRange(initiatedProcesses.Select(p => new UserActivityItemResponse
        {
            Type = "approval_initiated",
            VndId = p.Vnd?.Id,
            VndCode = p.Vnd?.Code,
            VndTitle = p.Vnd is { } vnd
                ? Resolve(vnd.TitleRu, vnd.TitleEn, vnd.TitleKg, languageCode) : null,
            Timestamp = p.PrimaryStartedAt,
            Description = "Инициировано согласование"
        }));

        return new UserActivityResponse
        {
            VndCreatedCount = vndCreatedCount,
            ApprovalsDecidedCount = approvalsDecidedCount,
            ApprovalsInitiatedCount = approvalsInitiatedCount,
            Recent = items.OrderByDescending(i => i.Timestamp).Take(recentLimit).ToList()
        };
    }

    private static string Resolve(string ru, string? en, string? kg, string lang) => lang switch
    {
        "en" => string.IsNullOrWhiteSpace(en) ? ru : en!,
        "kg" => string.IsNullOrWhiteSpace(kg) ? ru : kg!,
        _ => ru
    };

    private static DateTime LatestDecision(VndApprovalStage s)
    {
        var times = new[] { s.FinalHoldDecidedAt, s.RepeatDecidedAt, s.PrimaryDecidedAt };
        return times.Where(t => t.HasValue).Select(t => t!.Value).DefaultIfEmpty(s.UpdatedAt).Max();
    }

    private static string DescribeDecision(VndApprovalStage s)
    {
        var decision = s.FinalHoldDecision ?? s.RepeatDecision ?? s.PrimaryDecision;
        return decision switch
        {
            ApprovalStageDecision.Approved => "Согласовано",
            ApprovalStageDecision.ApprovedWithComment => "Согласовано с замечаниями",
            ApprovalStageDecision.Rejected => "Отклонено",
            ApprovalStageDecision.AutoApprovedByTimeout => "Согласовано автоматически (просрочка)",
            _ => "Решение по согласованию"
        };
    }
}
