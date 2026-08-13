using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Integrations.Directory;

/// <summary>Итог синхронизации — то, что администратор увидит после запуска.</summary>
public class DirectorySyncResult
{
    public int TotalInDirectory { get; set; }
    public List<string> Created { get; set; } = [];
    public List<string> Updated { get; set; } = [];
    public List<string> Deactivated { get; set; } = [];

    /// <summary>Записи каталога, которые синхронизировать не удалось, с причиной.</summary>
    public List<string> Skipped { get; set; } = [];

    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

public interface IDirectorySyncService
{
    Task<DirectorySyncResult> SyncAsync(int? actorUserId, CancellationToken ct = default);
}

/// <summary>
/// Синхронизация пользователей и оргструктуры со службой каталогов (INT-01).
///
/// Учётные записи из каталога не удаляются, а деактивируются: за сотрудником остаются
/// подписи, резолюции и поручения, и удаление порвало бы историю согласований.
///
/// Роли синхронизация не трогает вовсе. Права в системе выдаёт администратор банка по
/// матрице полномочий, а не групповая политика домена: одинаковые названия групп в AD
/// и ролей в СЭД совпадают редко, и автоматический перенос раздал бы лишний доступ.
/// </summary>
public class DirectorySyncService : IDirectorySyncService
{
    private readonly DelosferaDbContext _db;
    private readonly ILdapDirectory _directory;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly IAuditService _audit;
    private readonly LdapOptions _options;
    private readonly ILogger<DirectorySyncService> _logger;

    public DirectorySyncService(
        DelosferaDbContext db,
        ILdapDirectory directory,
        IUserPasswordHasher passwordHasher,
        IAuditService audit,
        IOptions<LdapOptions> options,
        ILogger<DirectorySyncService> logger)
    {
        _db = db;
        _directory = directory;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DirectorySyncResult> SyncAsync(int? actorUserId, CancellationToken ct = default)
    {
        var result = new DirectorySyncResult {StartedAt = DateTime.UtcNow};

        var entries = await _directory.ListUsersAsync(ct);
        result.TotalInDirectory = entries.Count;

        var units = await _db.OrganizationUnits.ToListAsync(ct);
        var positions = await _db.Positions.ToListAsync(ct);
        var defaultRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.TitleRu == _options.DefaultRoleTitleRu, ct);

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Email))
            {
                // Почта — идентификатор учётной записи в системе: без неё сотрудник
                // не сможет войти, и сопоставить его с существующей записью не с чем.
                result.Skipped.Add($"{entry.Login}: в каталоге не заполнен адрес почты");
                continue;
            }

            seenEmails.Add(entry.Email);

            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == entry.Email, ct);

            var unitId = ResolveUnit(entry.OrgUnit, units, result);
            var positionId = ResolvePosition(entry.Position, positions, result);

            if (user is null)
            {
                if (!_options.CreateMissingUsers)
                {
                    result.Skipped.Add($"{entry.Email}: заведение новых учётных записей выключено");
                    continue;
                }

                user = new User
                {
                    FullName = entry.FullName ?? entry.Login,
                    Email = entry.Email,
                    // Доменная учётная запись не имеет локального пароля: вход идёт
                    // привязкой к каталогу. Ставим случайный хеш, который заведомо
                    // не подойдёт ни к одной введённой строке.
                    PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
                    Source = UserSource.Ldap,
                    IsActive = !entry.IsDisabled,
                    OrgUnitId = unitId,
                    PositionId = positionId,
                };

                if (defaultRole is not null) user.Roles.Add(defaultRole);

                _db.Users.Add(user);
                result.Created.Add($"{entry.Email} — {user.FullName}");
                continue;
            }

            if (user.Source != UserSource.Ldap)
            {
                // Локальную учётную запись синхронизация не переписывает: её завёл
                // администратор руками, и каталог для неё не источник истины.
                result.Skipped.Add($"{entry.Email}: локальная учётная запись, не обновляется");
                continue;
            }

            var changed = false;

            if (entry.FullName is not null && user.FullName != entry.FullName)
            {
                user.FullName = entry.FullName;
                changed = true;
            }

            if (unitId is not null && user.OrgUnitId != unitId)
            {
                user.OrgUnitId = unitId;
                changed = true;
            }

            if (positionId is not null && user.PositionId != positionId)
            {
                user.PositionId = positionId;
                changed = true;
            }

            if (user.IsActive == entry.IsDisabled)
            {
                user.IsActive = !entry.IsDisabled;
                changed = true;
            }

            if (changed) result.Updated.Add($"{entry.Email} — {user.FullName}");
        }

        // Ушедшие из каталога: увольнение или перевод. Деактивируем, но не удаляем —
        // подписи и резолюции сотрудника остаются в истории документов.
        var stale = await _db.Users
            .Where(u => u.Source == UserSource.Ldap && u.IsActive)
            .ToListAsync(ct);

        foreach (var user in stale.Where(u => !seenEmails.Contains(u.Email)))
        {
            user.IsActive = false;
            result.Deactivated.Add($"{user.Email} — {user.FullName}");
        }

        await _db.SaveChangesAsync(ct);

        result.FinishedAt = DateTime.UtcNow;

        await _audit.LogAsync("Directory", 0, "Synced", actorUserId, new
        {
            total = result.TotalInDirectory,
            created = result.Created.Count,
            updated = result.Updated.Count,
            deactivated = result.Deactivated.Count,
            skipped = result.Skipped.Count,
        });

        _logger.LogInformation(
            "Синхронизация каталога: в каталоге {Total}, заведено {Created}, обновлено {Updated}, " +
            "деактивировано {Deactivated}, пропущено {Skipped}",
            result.TotalInDirectory, result.Created.Count, result.Updated.Count,
            result.Deactivated.Count, result.Skipped.Count);

        return result;
    }

    /// <summary>
    /// Должности справочника ведёт кадровая служба, и из каталога они не создаются:
    /// в title каталога встречается что угодно, вплоть до «и.о.». Ненайденная
    /// должность попадает в отчёт, а не теряется молча.
    /// </summary>
    private static int? ResolvePosition(string? title, List<Position> positions, DirectorySyncResult result)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var position = positions.FirstOrDefault(p =>
            p.TitleRu.Equals(title, StringComparison.OrdinalIgnoreCase));

        if (position is not null) return position.Id;

        result.Skipped.Add($"должность «{title}» не найдена в справочнике — сотрудник без должности");
        return null;
    }

    private int? ResolveUnit(string? title, List<OrganizationUnit> units, DirectorySyncResult result)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var unit = units.FirstOrDefault(u => u.TitleRu.Equals(title, StringComparison.OrdinalIgnoreCase));
        if (unit is not null) return unit.Id;

        if (!_options.CreateMissingOrgUnits)
        {
            result.Skipped.Add($"подразделение «{title}» не найдено в справочнике — сотрудник без привязки");
            return null;
        }

        unit = new OrganizationUnit {TitleRu = title};
        _db.OrganizationUnits.Add(unit);
        units.Add(unit);

        return null; // Id появится после сохранения — привязка подтянется следующим прогоном.
    }
}
