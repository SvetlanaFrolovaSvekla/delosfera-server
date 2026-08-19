using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Регламент и состояние согласия текущего сотрудника.</summary>
public class RegulationStateDto
{
    /// <summary>Регламент заведён и действует. Нет — согласие не спрашивается.</summary>
    public bool Required { get; set; }

    public bool Accepted { get; set; }

    public string? Version { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }

    public DateTime? AcceptedAt { get; set; }
}

public interface ISimpleSignatureRegulationService
{
    Task<RegulationStateDto> GetStateAsync(int userId, CancellationToken ct = default);
    Task<RegulationStateDto> AcceptAsync(int userId, string version, CancellationToken ct = default);
}

/// <summary>
/// Согласие с регламентом простой электронной подписи (Б-04).
///
/// Простая подпись имеет силу, только если стороны договорились о правилах её
/// применения. Согласие — и есть эта договорённость, поэтому его дают до того,
/// как человек впервые что-то подпишет, и повторяют при смене редакции.
/// </summary>
public class SimpleSignatureRegulationService : ISimpleSignatureRegulationService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;

    public SimpleSignatureRegulationService(DelosferaDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<RegulationStateDto> GetStateAsync(int userId, CancellationToken ct = default)
    {
        var regulation = await ActiveAsync(ct);
        if (regulation is null) return new RegulationStateDto {Required = false, Accepted = true};

        var consent = await _db.SimpleSignatureConsents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Version == regulation.Version, ct);

        return new RegulationStateDto
        {
            Required = true,
            Accepted = consent is not null,
            Version = regulation.Version,
            Title = regulation.Title,
            Body = regulation.Body,
            AcceptedAt = consent?.AcceptedAt,
        };
    }

    public async Task<RegulationStateDto> AcceptAsync(int userId, string version, CancellationToken ct = default)
    {
        var regulation = await ActiveAsync(ct)
            ?? throw new InvalidOperationException("Регламент применения простой подписи не заведён");

        // Соглашаются именно с той редакцией, которая сейчас показана. Если за время
        // чтения текст сменили, согласие относилось бы к другому документу.
        if (!string.Equals(version, regulation.Version, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Регламент обновился, пока вы читали. Откройте его заново и подтвердите согласие с новой редакцией");

        var already = await _db.SimpleSignatureConsents
            .AnyAsync(c => c.UserId == userId && c.Version == regulation.Version, ct);

        if (!already)
        {
            _db.SimpleSignatureConsents.Add(new SimpleSignatureConsent
            {
                UserId = userId,
                Version = regulation.Version,
                AcceptedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("SimpleSignatureRegulation", regulation.Id, "Accepted", userId,
                new {regulation.Version});
        }

        return await GetStateAsync(userId, ct);
    }

    private Task<SimpleSignatureRegulation?> ActiveAsync(CancellationToken ct) =>
        _db.SimpleSignatureRegulations
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
}
