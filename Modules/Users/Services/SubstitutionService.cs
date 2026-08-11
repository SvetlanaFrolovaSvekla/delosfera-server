using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public interface ISubstitutionService
{
    Task<List<SubstitutionDto>> ListAsync(int? userId);
    Task<SubstitutionDto> CreateAsync(SubstitutionCreateRequest request, int actorUserId);
    Task<SubstitutionDto> CancelAsync(int id, int actorUserId);

    /// <summary>
    /// Кого сейчас замещает пользователь: список id отсутствующих сотрудников.
    /// Используется выдачей задач, чтобы замещающий видел чужие поручения.
    /// </summary>
    Task<List<int>> GetActingForUserIdsAsync(int substituteUserId);
}

/// <summary>
/// Замещение на период отсутствия (GEN-14). Задачи отсутствующего переходят
/// замещающему на время периода и возвращаются сами, когда период кончился.
/// </summary>
public class SubstitutionService : ISubstitutionService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;

    public SubstitutionService(DelosferaDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<SubstitutionDto>> ListAsync(int? userId)
    {
        var q = _db.Substitutions
            .Include(s => s.User)
            .Include(s => s.SubstituteUser)
            .AsQueryable();

        if (userId is { } id)
            q = q.Where(s => s.UserId == id || s.SubstituteUserId == id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await q
            .OrderByDescending(s => s.StartsOn)
            .Select(s => new SubstitutionDto
            {
                Id = s.Id,
                UserId = s.UserId,
                UserName = s.User!.FullName,
                SubstituteUserId = s.SubstituteUserId,
                SubstituteUserName = s.SubstituteUser!.FullName,
                StartsOn = s.StartsOn,
                EndsOn = s.EndsOn,
                Reason = s.Reason,
                IsCancelled = s.IsCancelled,
                IsActive = !s.IsCancelled && s.StartsOn <= today && s.EndsOn >= today,
            })
            .ToListAsync();
    }

    public async Task<SubstitutionDto> CreateAsync(SubstitutionCreateRequest request, int actorUserId)
    {
        if (request.UserId == request.SubstituteUserId)
            throw new ArgumentException("Сотрудник не может замещать сам себя");

        if (request.EndsOn < request.StartsOn)
            throw new ArgumentException("Дата окончания замещения раньше даты начала");

        foreach (var id in new[] {request.UserId, request.SubstituteUserId})
            if (!await _db.Users.AnyAsync(u => u.Id == id))
                throw new KeyNotFoundException($"Пользователь {id} не найден");

        // Пересечение периодов у одного сотрудника означало бы, что задачи уходят
        // сразу двоим — кто принял решение, восстановить будет нельзя.
        var overlaps = await _db.Substitutions.AnyAsync(s =>
            s.UserId == request.UserId && !s.IsCancelled
            && s.StartsOn <= request.EndsOn && s.EndsOn >= request.StartsOn);

        if (overlaps)
            throw new InvalidOperationException(
                "На этот период у сотрудника уже оформлено замещение");

        var entity = new Substitution
        {
            UserId = request.UserId,
            SubstituteUserId = request.SubstituteUserId,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            Reason = request.Reason?.Trim(),
        };

        _db.Substitutions.Add(entity);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Substitution", entity.Id, "Created", actorUserId, new
        {
            entity.UserId,
            entity.SubstituteUserId,
            entity.StartsOn,
            entity.EndsOn,
        });

        return (await ListAsync(null)).First(s => s.Id == entity.Id);
    }

    public async Task<SubstitutionDto> CancelAsync(int id, int actorUserId)
    {
        var entity = await _db.Substitutions.FirstOrDefaultAsync(s => s.Id == id)
                     ?? throw new KeyNotFoundException("Замещение не найдено");

        if (entity.IsCancelled)
            throw new InvalidOperationException("Замещение уже отменено");

        entity.IsCancelled = true;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Substitution", entity.Id, "Cancelled", actorUserId, null);

        return (await ListAsync(null)).First(s => s.Id == entity.Id);
    }

    public async Task<List<int>> GetActingForUserIdsAsync(int substituteUserId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _db.Substitutions
            .Where(s => s.SubstituteUserId == substituteUserId && !s.IsCancelled
                        && s.StartsOn <= today && s.EndsOn >= today)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();
    }
}
