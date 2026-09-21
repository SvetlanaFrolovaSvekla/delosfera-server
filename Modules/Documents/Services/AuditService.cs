using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Журнал аудита с доказуемой неизменяемостью (AUD-1, tamper-evident).
///
/// Каждая запись несёт SHA-256 от своих существенных полей и хеша предыдущей записи —
/// получается цепочка (blockchain-подобная): ретроактивная правка или удаление любой
/// записи рвёт цепь у всех последующих, и проверка это обнаруживает. Добавление
/// сериализуется advisory-локом Postgres, иначе две параллельные записи сослались бы на
/// один и тот же prev и цепь раздвоилась бы (уникальный индекс на Hash — второй рубеж).
///
/// Если операция уже открыла транзакцию, запись аудита идёт в ней же (атомарно с
/// операцией); иначе служба открывает свою короткую транзакцию под advisory-лок.
/// </summary>
public class AuditService : IAuditService
{
    private readonly DelosferaDbContext _db;

    public AuditService(DelosferaDbContext db) => _db = db;

    /// <summary>Константа advisory-лока для сериализации добавления в цепь аудита.</summary>
    private const long ChainLockKey = 4917342001;

    public async Task LogAsync(string entityType, int entityId, string action, int? userId, object? payload = null)
    {
        var entry = new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            At = DateTime.UtcNow,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
        };

        // Операция уже в транзакции → пишем аудит в неё же (атомарно с самой операцией).
        if (_db.Database.CurrentTransaction is not null)
        {
            await AppendChainedAsync(entry);
            return;
        }

        // Иначе — своя короткая транзакция под advisory-лок, с учётом стратегии повторов Npgsql.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await AppendChainedAsync(entry);
            await tx.CommitAsync();
        });
    }

    /// <summary>Добавить запись в конец цепи. Требует активной транзакции (advisory-лок держится до её конца).</summary>
    private async Task AppendChainedAsync(AuditEntry entry)
    {
        // Сериализуем добавление: без этого два параллельных append прочитали бы один prev.
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ChainLockKey})");

        var prevHash = await _db.AuditEntries
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync();

        entry.PrevHash = prevHash;
        entry.Hash = ComputeHash(entry, prevHash);

        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Проверка целостности цепи: пересчитывает хеши по порядку и сверяет со связями.
    /// Возвращает ok=true и число проверенных записей, либо первый Id, где цепь нарушена.
    /// Легаси-записи без хеша (до бэкфилла) пропускаются как «дочейновые».
    /// </summary>
    public async Task<AuditChainStatus> VerifyChainAsync(CancellationToken ct = default)
    {
        const int batch = 1000;
        string? expectedPrev = null;
        var checkedCount = 0;
        var lastId = 0L;

        while (true)
        {
            var rows = await _db.AuditEntries.AsNoTracking()
                .Where(a => a.Id > lastId)
                .OrderBy(a => a.Id)
                .Take(batch)
                .ToListAsync(ct);
            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (row.Hash is null)
                {
                    // Дочейновое легаси — цепь ещё не построена по этой записи, пропускаем.
                    lastId = row.Id;
                    continue;
                }

                if (checkedCount > 0 && row.PrevHash != expectedPrev)
                    return AuditChainStatus.Broken(row.Id, "разрыв связи с предыдущей записью");

                if (ComputeHash(row, row.PrevHash) != row.Hash)
                    return AuditChainStatus.Broken(row.Id, "хеш не совпадает — запись изменена");

                expectedPrev = row.Hash;
                checkedCount++;
                lastId = row.Id;
            }
        }

        return AuditChainStatus.Ok(checkedCount);
    }

    /// <summary>
    /// Однократно достроить хеш-цепь по записям без хеша (бэкфилл легаси, AUD-1). Идёт по
    /// возрастанию Id под advisory-локом. Возвращает число заполненных записей.
    /// </summary>
    public async Task<int> BackfillChainAsync(CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ChainLockKey})", ct);

            // Хеш последней уже сцепленной записи — старт для продолжения цепи.
            var prevHash = await _db.AuditEntries
                .Where(a => a.Hash != null)
                .OrderByDescending(a => a.Id)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync(ct);

            var filled = 0;
            const int batch = 500;

            while (true)
            {
                var rows = await _db.AuditEntries
                    .Where(a => a.Hash == null)
                    .OrderBy(a => a.Id)
                    .Take(batch)
                    .ToListAsync(ct);
                if (rows.Count == 0) break;

                foreach (var row in rows)
                {
                    row.PrevHash = prevHash;
                    row.Hash = ComputeHash(row, prevHash);
                    prevHash = row.Hash;
                    filled++;
                }

                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return filled;
        });
    }

    /// <summary>
    /// Канонический хеш записи. Разделитель  (Unit Separator) не встречается в тексте,
    /// поэтому поля не «склеиваются» неоднозначно. Дата — в UTC ISO-8601 «O».
    /// </summary>
    private static string ComputeHash(AuditEntry e, string? prevHash)
    {
        var canonical = string.Join('',
            prevHash ?? "",
            e.EntityType,
            e.EntityId.ToString(),
            e.Action,
            e.UserId?.ToString() ?? "",
            e.At.ToUniversalTime().ToString("O"),
            e.PayloadJson ?? "");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
