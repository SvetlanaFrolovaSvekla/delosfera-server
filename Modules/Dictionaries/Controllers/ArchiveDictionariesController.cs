using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Dictionaries.Controllers;

/// <summary>
/// Справочники архивного хранения (GEN-09): сроки хранения и номенклатура дел.
/// Пока только чтение — списки нужны формам подшивки; ведение справочников
/// администратором добавляется отдельно, когда придёт ответ по вопросу В-2.
/// </summary>
[ApiController]
[Route("api/dictionaries")]
[Tags("Справочники — Архив")]
[Authorize]
public class ArchiveDictionariesController : ControllerBase
{
    private readonly DelosferaDbContext _db;

    public ArchiveDictionariesController(DelosferaDbContext db) => _db = db;

    /// <summary>Сроки хранения документов.</summary>
    [HttpGet("storage-term")]
    public async Task<IActionResult> StorageTerms()
    {
        var items = await _db.StorageTerms.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Years == null ? int.MaxValue : x.Years)
            .Select(x => new { x.Id, x.Code, x.TitleRu, x.TitleEn, x.TitleKg, x.Years, x.IsActive })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Дела номенклатуры; по умолчанию — открытые для подшивки.</summary>
    [HttpGet("nomenclature-case")]
    public async Task<IActionResult> Cases([FromQuery] int? year = null, [FromQuery] bool includeClosed = false)
    {
        var query = _db.NomenclatureCases.AsNoTracking().Where(x => x.IsActive);

        if (year is int y) query = query.Where(x => x.Year == y);
        // Закрытое дело в форме подшивки только мешает — показываем по явному запросу.
        if (!includeClosed) query = query.Where(x => x.ClosedOn == null);

        var items = await query
            .OrderByDescending(x => x.Year).ThenBy(x => x.Index)
            .Select(x => new
            {
                x.Id, x.Index, x.TitleRu, x.TitleEn, x.TitleKg, x.Year,
                x.OrgUnitId, x.StorageTermId, x.ClosedOn, x.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }
}
