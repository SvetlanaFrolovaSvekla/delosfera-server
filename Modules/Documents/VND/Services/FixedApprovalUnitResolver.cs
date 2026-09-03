using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Какое подразделение отвечает за фиксированный этап согласования ВНД.
///
/// Номера подразделений были прописаны в коде, и это подвело: «Управление
/// методологии» оказалось нашим дубликатом портального «Отдела методологии»,
/// при слиянии справочника люди и документы переехали на портальную запись, а
/// проверка продолжала ждать прежний номер — согласующий из методологии не
/// проходил собственный этап.
///
/// Портал владеет этими подразделениями, и его номер у записи не меняется, куда
/// бы её ни перенесли внутри нашего справочника. Поэтому ищем по нему, а
/// прежний внутренний номер остаётся запасным — для базы, где синхронизации с
/// порталом ещё не было.
/// </summary>
public interface IFixedApprovalUnitResolver
{
    /// <summary>Подразделение этапа; null — если такого этапа нет среди фиксированных.</summary>
    Task<int?> ResolveAsync(ApprovalStageKind kind, CancellationToken ct = default);
}

public class FixedApprovalUnitResolver : IFixedApprovalUnitResolver
{
    /// <summary>Номер подразделения в портале банка и наш номер как запасной.</summary>
    private static readonly Dictionary<ApprovalStageKind, (int Portal, int Local)> Units = new()
    {
        [ApprovalStageKind.Legal] = (39, FixedApprovalOrgUnits.LegalOrgUnitId),
        [ApprovalStageKind.RiskManagement] = (56, FixedApprovalOrgUnits.RiskManagementOrgUnitId),
        [ApprovalStageKind.Compliance] = (64, FixedApprovalOrgUnits.ComplianceOrgUnitId),
        [ApprovalStageKind.Methodology] = (65, FixedApprovalOrgUnits.MethodologyOrgUnitId),
    };

    private readonly DelosferaDbContext _db;

    public FixedApprovalUnitResolver(DelosferaDbContext db) => _db = db;

    public async Task<int?> ResolveAsync(ApprovalStageKind kind, CancellationToken ct = default)
    {
        if (!Units.TryGetValue(kind, out var unit)) return null;

        var byPortal = await _db.OrganizationUnits.AsNoTracking()
            .Where(u => u.ExternalId == unit.Portal)
            .Select(u => (int?) u.Id)
            .FirstOrDefaultAsync(ct);

        if (byPortal is not null) return byPortal;

        // Портальной записи нет — синхронизация ещё не проходила. Возвращаем
        // прежний номер, но только если подразделение с ним существует: иначе
        // проверка сравнивала бы согласующего с несуществующим подразделением.
        var localExists = await _db.OrganizationUnits.AsNoTracking()
            .AnyAsync(u => u.Id == unit.Local, ct);

        return localExists ? unit.Local : null;
    }
}
