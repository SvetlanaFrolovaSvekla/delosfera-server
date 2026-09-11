using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using Microsoft.AspNetCore.Authorization;

namespace delosfera_server.Modules.Documents.VND.Controllers;

[ApiController]
[Route("api/vnd")]
[Tags("ВНД")]
[Authorize]
public class VndController : ControllerBase
{
    private readonly IVndService _service;
    private readonly ILanguageResolver _languageResolver;
    private readonly ICurrentUserService _currentUser;

    public VndController(IVndService service, ILanguageResolver languageResolver, ICurrentUserService currentUser)
    {
        _service = service;
        _languageResolver = languageResolver;
        _currentUser = currentUser;
    }

    /// <summary>Добавление новой ВНД</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VndResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndResponse>> Create([FromBody] CreateVndRequest request)
    {
        try
        {
            var language = _languageResolver.Resolve(Request);
            var result = await _service.CreateAsync(request, _currentUser.UserId, language);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>Расширенный поиск ВНД по всем фильтрам</summary>
    [HttpPost("search")]
    [RequirePermission(PermissionCode.ViewVnd)]
    public async Task<ActionResult<List<VndResponse>>> Search([FromBody] VndSearchRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.SearchAsync(request, language);
        return Ok(result);
    }

    /// <summary>Сводка по срокам актуализации: сколько документов в норме,
    /// с приближающимся сроком, критичных и просроченных. Для дашборда планирования актуализации.</summary>
    [HttpGet("actualization/summary")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    [ProducesResponseType(typeof(VndActualizationSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationSummaryResponse>> GetActualizationSummary()
    {
        return Ok(await _service.GetActualizationSummaryAsync());
    }

    /// <summary>Экспорт таблицы страницы "Планирование актуализации" в Excel — кнопка "Экспорт
    /// плана в Excel". Модалка на фронте даёт донастроить те же фильтры, что и на странице
    /// (request.Filter), и выбрать нужные колонки (request.Columns); обязательные колонки
    /// экспортируются всегда. Право доступа — то же самое, что и у обычного поиска (Search
    /// выше), а не ViewVndActualizationPage: экспорт отдаёт те же строки ВНД, что вернул бы
    /// поиск с тем же фильтром, и не должен требовать меньших прав, чем сам поиск.</summary>
    [HttpPost("actualization/export")]
    [RequirePermission(PermissionCode.ViewVnd)]
    public async Task<IActionResult> ExportActualizationPlan([FromBody] VndActualizationExportRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var bytes = await _service.ExportActualizationPlanAsync(request, language);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Планирование актуализации.xlsx");
    }

    /// <summary>Получить один ВНД по id</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCode.ViewVnd)]
    public async Task<ActionResult<VndResponse>> GetById(int id)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            return Ok(await _service.GetByIdAsync(id, language));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Удаление ВНД (только черновик, создателем или главным редактором)</summary>
    /// <response code="204">ВНД удалён</response>
    /// <response code="409">Удалять можно только черновик</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.DeleteVnd)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Архивировать (отменить) ВНД — кнопка "Архивировать". Доступно на любом статусе,
    /// кроме черновика (тот только удаляется, см. Delete выше) и уже архивированного. Если
    /// документ на согласовании — согласование отзывается автоматически в рамках той же
    /// операции.</summary>
    /// <response code="200">ВНД архивирован</response>
    /// <response code="409">Архивировать можно только не-черновик и не уже архивированный документ</response>
    [HttpPost("{id:int}/cancel")]
    [RequirePermission(PermissionCode.CancelVnd)]
    [ProducesResponseType(typeof(VndResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VndResponse>> Cancel(int id, [FromBody] CancelVndRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            return Ok(await _service.CancelAsync(id, request, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Добавление новой редакции ВНД</summary>
    [HttpPost("{vndId:int}/redactions")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndRedactionResponse>> AddRedaction(
        int vndId, [FromForm] CreateVndRedactionRequest request)
    {
        try
        {
            var result = await _service.AddRedactionAsync(vndId, request, _currentUser.UserId);
            return CreatedAtAction(nameof(GetById), new { id = vndId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Получить список редакций ВНД</summary>
    [HttpGet("{vndId:int}/redactions")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(List<VndRedactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<VndRedactionResponse>>> GetRedactions(int vndId)
    {
        try
        {
            return Ok(await _service.GetRedactionsAsync(vndId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Отправить черновик редакции (редакция со статусом "Требуется согласование"
    /// на согласование</summary>
    // Точки «отправить редакцию на согласование» здесь больше нет. Она меняла
    // статус редакции на «на согласовании», не заводя самого согласования, — а
    // запуск согласования требует черновика. Нажавший её оставлял редакцию в
    // состоянии, из которого согласование уже не запускалось: документ отвечал
    // «отправлено», и не двигался никуда. Согласование начинается сразу с
    // указанием состава: POST /api/vnd/{vndId}/approval/start.

    /// <summary>Только для главного редактора: сделать черновик редакции действующим/текущим
    /// напрямую, минуя согласование целиком (кнопка "Сделать актуальной редакцией без
    /// согласования" рядом с обычной "Отправить на согласование").</summary>
    [HttpPost("{vndId:int}/redactions/{redactionId:int}/publish-without-approval")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndRedactionResponse>> PublishRedactionWithoutApproval(int vndId, int redactionId)
    {
        try
        {
            return Ok(await _service.PublishRedactionWithoutApprovalAsync(vndId, redactionId, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Обновление реквизитов ВНД (кнопка "Изменить реквизиты")</summary>
    [HttpPut("{id:int}/requisites")]
    [RequirePermission(PermissionCode.EditVndRequisites)]
    [ProducesResponseType(typeof(VndResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VndResponse>> UpdateRequisites(int id, [FromBody] UpdateVndRequisitesRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            return Ok(await _service.UpdateRequisitesAsync(id, request, language));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Связи ВНД: ссылки на другие документы и документы, ссылающиеся на этот</summary>
    [HttpGet("{vndId:int}/links")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndLinksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndLinksResponse>> GetLinks(int vndId)
    {
        var language = _languageResolver.Resolve(Request);
        try { return Ok(await _service.GetLinksAsync(vndId, language)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Добавить ссылку на другой (только действующий) ВНД</summary>
    [HttpPost("{vndId:int}/links")]
    [RequirePermission(PermissionCode.EditVndRequisites)]
    [ProducesResponseType(typeof(VndLinkResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<VndLinkResponse>> AddLink(int vndId, [FromBody] AddVndLinkRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            var result = await _service.AddLinkAsync(vndId, request, language);
            return CreatedAtAction(nameof(GetLinks), new { vndId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Удалить связь ВНД (можно с любой из сторон связи)</summary>
    [HttpDelete("{vndId:int}/links/{linkId:int}")]
    [RequirePermission(PermissionCode.EditVndRequisites)]
    public async Task<IActionResult> DeleteLink(int vndId, int linkId)
    {
        try
        {
            await _service.DeleteLinkAsync(vndId, linkId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Разрешить легаси-ссылку db://documents/{code} или db://attachments/{n},
    /// унаследованную из старой системы (isrib) и встречающуюся в тексте документа как обычная
    /// внешняя гиперссылка (открывала пустую страницу) — см. VndService.ResolveLegacyLinkAsync
    /// и useDocxLegacyLinks на фронте (клик по такой ссылке внутри отрендеренного docx).</summary>
    [HttpGet("{vndId:int}/legacy-link")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(LegacyLinkResolveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegacyLinkResolveResponse>> ResolveLegacyLink(
        int vndId, [FromQuery] string type, [FromQuery] string legacyId)
    {
        try
        {
            return Ok(await _service.ResolveLegacyLinkAsync(vndId, type, legacyId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Прямое редактирование последней редакции (подмена файлов/описания) - без согласования,
    /// без создания новой редакции, без изменения даты актуализации. Раньше требовало право
    /// EditLastRevisionDirectly безусловно; теперь оно даёт безусловный доступ (любая редакция,
    /// включая уже действующую), а без него сервис (см. EditRedactionDirectlyCoreAsync) всё равно
    /// пускает разработчика/куратора/ответственного/инициатора/главного редактора редактировать
    /// СВОЙ ещё не согласованный черновик (в т.ч. вернувшийся в черновик после отклонения на
    /// согласовании) - иначе им попросту нечем исправить отклонённый документ. Оставлен для
    /// обратной совместимости - см. более общий EditRedactionDirectly ниже, который работает для
    /// любой редакции, не только последней.</summary>
    [HttpPut("{vndId:int}/redactions/last")]
    [Consumes("multipart/form-data")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndRedactionResponse>> EditLastRevisionDirectly(
        int vndId, [FromForm] EditLastRevisionDirectlyRequest request)
    {
        try
        {
            return Ok(await _service.EditLastRevisionDirectlyAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Прямое редактирование ЛЮБОЙ редакции (подмена основных файлов, специальных
    /// вложений — ТИД/Лист согласования/Матрица разногласий — и/или описания) - без согласования,
    /// без создания новой редакции, без изменения даты актуализации. Тот же паттерн, что и
    /// EditLastRevisionDirectly выше (см. её комментарий про разграничение прав), но не ограничен
    /// последней редакцией - см. RedactionsSidebar на фронте, где кнопка "Редактировать" теперь
    /// показывается у любой редакции.</summary>
    [HttpPut("{vndId:int}/redactions/{redactionId:int}/edit-directly")]
    [Consumes("multipart/form-data")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndRedactionResponse>> EditRedactionDirectly(
        int vndId, int redactionId, [FromForm] EditLastRevisionDirectlyRequest request)
    {
        try
        {
            return Ok(await _service.EditRedactionDirectlyAsync(vndId, redactionId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Кнопка "Сформировать или загрузить ТИД" — прикладывает файл ТИД к черновику
    /// последней редакции отдельным шагом (поле ТИД убрано из формы загрузки редакции).</summary>
    [HttpPut("{vndId:int}/redactions/last/tid")]
    [Consumes("multipart/form-data")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VndRedactionResponse>> UploadTidForLastRedaction(
        int vndId, [FromForm] UploadRedactionTidRequest request)
    {
        try
        {
            return Ok(await _service.UploadTidForLastRedactionAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Быстрый поиск ВНД для строки поиска в шапке — по коду и названию (RU/EN/KG)</summary>
    [HttpGet("quick-search")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(List<VndQuickSearchResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndQuickSearchResponse>>> QuickSearch(
        [FromQuery] string q, [FromQuery] int limit = 8)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.QuickSearchAsync(q, language, limit);
        return Ok(result);
    }
}
