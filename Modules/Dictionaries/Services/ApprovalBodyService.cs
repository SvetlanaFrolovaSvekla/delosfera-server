using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class ApprovalBodyService : IApprovalBodyService
{
    // Максимальная глубина вложенности - защита от случайных бесконечных цепочек,
    // с запасом покрывает любую реальную оргструктуру банка
    private const int MaxDepth = 5;

    private readonly DelosferaDbContext _db;

    public ApprovalBodyService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<ApprovalBodyResponse>> GetAllAsync(ApprovalBodySortBy sortBy, string? search,
        string languageCode)
    {
        IQueryable<ApprovalBody> query = _db.ApprovalBodies;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleRu, $"%{term}%") ||
                (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{term}%")) ||
                (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{term}%")));
        }

        query = sortBy switch
        {
            ApprovalBodySortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            ApprovalBodySortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            ApprovalBodySortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            ApprovalBodySortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<ApprovalBodyResponse> CreateAsync(CreateApprovalBodyRequest request, string languageCode)
    {
        if (request.ParentId.HasValue)
        {
            await HierarchyValidation.EnsureParentExistsAsync(_db.ApprovalBodies, request.ParentId.Value,
                pid => $"Родительский орган утверждения с id={pid} не найден");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.ApprovalBodies, request.ParentId.Value, MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        var entity = new ApprovalBody
        {
            TitleRu = request.TitleRu, TitleEn = request.TitleEn, TitleKg = request.TitleKg,
            ParentId = request.ParentId
        };

        _db.ApprovalBodies.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<ApprovalBodyResponse> UpdateAsync(int id, UpdateApprovalBodyRequest request, string languageCode)
    {
        var entity = await _db.ApprovalBodies.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Орган утверждения с id={id} не найден");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Орган утверждения не может быть родителем самого себя");

            await HierarchyValidation.EnsureParentExistsAsync(_db.ApprovalBodies, request.ParentId.Value,
                pid => $"Родительский орган утверждения с id={pid} не найден");
            await HierarchyValidation.EnsureNoCircularReferenceAsync(_db.ApprovalBodies, id, request.ParentId.Value,
                "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.ApprovalBodies, request.ParentId.Value, MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.ParentId = request.ParentId;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.ApprovalBodies.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Орган утверждения с id={id} не найден");

        var hasChildren = await _db.ApprovalBodies.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException(
                "Нельзя удалить орган утверждения — у него есть дочерние записи. Сначала удалите или перенесите их");

        _db.ApprovalBodies.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить орган утверждения — на него есть ссылки в других документах");
        }
    }


    private static ApprovalBodyResponse ToResponse(ApprovalBody entity, string languageCode) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        ParentId = entity.ParentId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}