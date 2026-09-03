using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Hr.Controllers;

/// <summary>Заведение листа ознакомления по кадровому приказу.</summary>
public class HrAcknowledgementRequest
{
    public string? Instruction { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Кого знакомить. Пусто — те, кого приказ касается.</summary>
    public Documents.Services.AcknowledgementTargets? Targets { get; set; }
}

public class HrOrderEmployeeRequest
{
    public int UserId { get; set; }

    /// <summary>Реквизиты по виду приказа — JSON, набор полей задаётся видом.</summary>
    public System.Text.Json.JsonElement? Fields { get; set; }
}

public class HrOrderSaveRequest
{
    public HrOrderKind Kind { get; set; } = HrOrderKind.Other;
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? Basis { get; set; }

    public DateOnly? OrderDate { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public int? SignerUserId { get; set; }
    public int? SourceSzId { get; set; }
    public int? CancelsOrderId { get; set; }
    public int? NomenclatureCaseId { get; set; }

    public List<HrOrderEmployeeRequest> Employees { get; set; } = [];
}

/// <summary>
/// Приказы по личному составу.
///
/// Записка — просьба, приказ — решение по ней. Между «прошу направить в
/// командировку» и фактом командировки стоит подписанный и зарегистрированный
/// приказ, с которым сотрудника знакомят под роспись.
/// </summary>
[ApiController]
[Authorize]
[Route("api/hr/orders")]
[Tags("Кадровые приказы")]
public class HrOrderController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentHtmlService _html;
    private readonly Documents.Services.IAcknowledgementService _acknowledgements;
    private readonly Files.Services.IFileStorageService _storage;

    public HrOrderController(
        DelosferaDbContext db, ICurrentUserService currentUser, IDocumentHtmlService html,
        Documents.Services.IAcknowledgementService acknowledgements,
        Files.Services.IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _html = html;
        _acknowledgements = acknowledgements;
        _storage = storage;
    }

    /// <summary>
    /// Виды приказов и схемы их полей.
    ///
    /// Схема берётся от кадровых записок, а не пишется заново: у приказа о
    /// командировке те же реквизиты, что у записки о ней — город, срок, цель.
    /// Записка просит, приказ решает, но описывают они одно и то же событие.
    ///
    /// Виды, которым в записках соответствия нет — увольнение, взыскание,
    /// премия, — идут без схемы: их реквизиты пишутся в тексте приказа.
    /// </summary>
    [HttpGet("kinds")]
    public IActionResult Kinds() =>
        Ok(Enum.GetValues<HrOrderKind>().Select(kind => new
        {
            kind = kind.ToString(),
            title = KindTitle(kind),
            formKey = SchemaKey(kind),
        }));

    private static string KindTitle(HrOrderKind kind) => kind switch
    {
        HrOrderKind.Hiring => "Приём на работу",
        HrOrderKind.Transfer => "Перевод",
        HrOrderKind.Dismissal => "Увольнение",
        HrOrderKind.Leave => "Отпуск",
        HrOrderKind.BusinessTrip => "Командировка",
        HrOrderKind.Salary => "Изменение оклада",
        HrOrderKind.Bonus => "Премирование",
        HrOrderKind.Discipline => "Дисциплинарное взыскание",
        HrOrderKind.Training => "Обучение",
        HrOrderKind.Combination => "Совмещение, замещение",
        _ => "Иное",
    };

    /// <summary>Схема полей из справочника кадровых записок. Пусто — своих полей нет.</summary>
    private static string? SchemaKey(HrOrderKind kind) => kind switch
    {
        HrOrderKind.Hiring => "Приём на работу",
        HrOrderKind.Transfer => "Перемещение без изменения оклада",
        HrOrderKind.BusinessTrip => "Командировка",
        HrOrderKind.Salary => "Изменение оклада",
        _ => null,
    };

    /// <summary>Книга приказов с фильтрами.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ViewHrOrders)]
    public async Task<IActionResult> Index(
        [FromQuery] HrOrderKind? kind,
        [FromQuery] HrOrderStatus? status,
        [FromQuery] int? userId,
        [FromQuery] int? year,
        [FromQuery] string? text,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.HrOrders.AsNoTracking().AsQueryable();

        if (kind is { } k) query = query.Where(o => o.Kind == k);
        if (status is { } s) query = query.Where(o => o.Status == s);
        if (year is { } y) query = query.Where(o => o.Year == y);
        if (userId is { } u) query = query.Where(o => o.Employees.Any(e => e.UserId == u));

        if (!string.IsNullOrWhiteSpace(text))
        {
            var needle = text.Trim();
            query = query.Where(o =>
                EF.Functions.ILike(o.Title, $"%{needle}%")
                || (o.RegNumber != null && EF.Functions.ILike(o.RegNumber, $"%{needle}%")));
        }

        var total = await query.CountAsync(ct);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var items = await query
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                Kind = o.Kind.ToString(),
                Status = o.Status.ToString(),
                o.RegNumber,
                o.OrderDate,
                o.EffectiveFrom,
                o.EffectiveTo,
                o.Title,
                o.Basis,
                Signer = o.SignerUser == null ? null : o.SignerUser.FullName,
                o.SignedAt,
                o.AcknowledgementSheetId,
                FileCount = o.Files.Count,
                Employees = o.Employees.Select(e => new
                {
                    e.UserId,
                    Name = e.FullNameSnapshot ?? (e.User == null ? null : e.User.FullName),
                    e.PositionSnapshot,
                    e.UnitSnapshot,
                }).ToList(),
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCode.ViewHrOrders)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var order = await _db.HrOrders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id,
                Kind = o.Kind.ToString(),
                Status = o.Status.ToString(),
                o.RegNumber, o.Year, o.OrderDate, o.EffectiveFrom, o.EffectiveTo,
                o.Title, o.Body, o.Basis,
                o.SourceSzId, o.CancelsOrderId, o.NomenclatureCaseId,
                o.SignerUserId,
                Signer = o.SignerUser == null ? null : o.SignerUser.FullName,
                o.SignedAt, o.AcknowledgementSheetId,
                FileCount = o.Files.Count,
                Employees = o.Employees.Select(e => new
                {
                    e.Id, e.UserId,
                    Name = e.FullNameSnapshot ?? (e.User == null ? null : e.User.FullName),
                    e.PositionSnapshot, e.UnitSnapshot, e.FieldValues,
                }).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> Create([FromBody] HrOrderSaveRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var now = DateTime.UtcNow;
        var orderDate = request.OrderDate ?? DateOnly.FromDateTime(now);

        var order = new HrOrder
        {
            Kind = request.Kind,
            Status = HrOrderStatus.Draft,
            Year = orderDate.Year,
            OrderDate = orderDate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Title = request.Title.Trim(),
            // Текст приказа приходит из редактора разметкой и попадёт в браузер
            // другого сотрудника — чистим по тому же белому списку, что и записки.
            Body = _html.Sanitize(request.Body),
            Basis = Trim(request.Basis),
            SourceSzId = request.SourceSzId,
            CancelsOrderId = request.CancelsOrderId,
            SignerUserId = request.SignerUserId,
            NomenclatureCaseId = request.NomenclatureCaseId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await FillEmployeesAsync(order, request.Employees, ct);

        _db.HrOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = order.Id });
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> Update(int id, [FromBody] HrOrderSaveRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var order = await _db.HrOrders
            .Include(o => o.Employees)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null) return NotFound();

        // Подписанный приказ не правят: он уже доведён до сотрудника под роспись,
        // и правка задним числом означала бы, что ознакомили не с тем.
        if (order.Status == HrOrderStatus.Signed)
            return Conflict(new { message = "Приказ подписан — изменить его нельзя, издайте новый." });

        var orderDate = request.OrderDate ?? order.OrderDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        order.Kind = request.Kind;
        order.Year = orderDate.Year;
        order.OrderDate = orderDate;
        order.EffectiveFrom = request.EffectiveFrom;
        order.EffectiveTo = request.EffectiveTo;
        order.Title = request.Title.Trim();
        order.Body = _html.Sanitize(request.Body);
        order.Basis = Trim(request.Basis);
        order.SourceSzId = request.SourceSzId;
        order.CancelsOrderId = request.CancelsOrderId;
        order.SignerUserId = request.SignerUserId;
        order.NomenclatureCaseId = request.NomenclatureCaseId;
        order.UpdatedAt = DateTime.UtcNow;

        _db.HrOrderEmployees.RemoveRange(order.Employees);
        order.Employees.Clear();
        await FillEmployeesAsync(order, request.Employees, ct);

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Подписать: присвоить номер по книге и закрыть от правки. Ознакомление
    /// заводится отдельно — листом на тех, кого приказ касается.
    /// </summary>
    [HttpPost("{id:int}/sign")]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> Sign(int id, CancellationToken ct)
    {
        var order = await _db.HrOrders
            .Include(o => o.Employees)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null) return NotFound();

        if (order.Status == HrOrderStatus.Signed)
            return Conflict(new { message = "Приказ уже подписан." });

        if (order.Employees.Count == 0)
            return BadRequest(new { message = "В приказе нет ни одного сотрудника." });

        order.RegNumber ??= await NextNumberAsync(order.Year, ct);
        order.Status = HrOrderStatus.Signed;
        order.SignerUserId ??= _currentUser.UserId;
        order.SignedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        // Приказ об отмене гасит отменяемый: ссылка на него сохранялась, а сам
        // отменённый приказ оставался «Подписан» — в реестре и в кадровой истории
        // сотрудника отменённая командировка выглядела действующей. Статус
        // «Отменён» был объявлен и не выставлялся нигде.
        //
        // Момент — подписание, а не создание: пока приказ черновик, он ничего не
        // отменяет, и передумать ещё можно.
        if (order.CancelsOrderId is int cancelledId)
        {
            var cancelled = await _db.HrOrders.FirstOrDefaultAsync(o => o.Id == cancelledId, ct);

            if (cancelled is not null && cancelled.Status != HrOrderStatus.Cancelled)
            {
                cancelled.Status = HrOrderStatus.Cancelled;
                cancelled.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { order.RegNumber, order.SignedAt });
    }

    /// <summary>
    /// Приложить скан подписанного приказа.
    ///
    /// Приказ по личному составу подписывают на бумаге и подшивают в личное дело;
    /// без скана карточка оставалась записью о приказе, а не самим приказом — и на
    /// просьбу «покажите подписанный» ответить было нечем.
    /// </summary>
    [HttpPost("{id:int}/files")]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> AddFile(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл не выбран." });

        var order = await _db.HrOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        var stored = await _storage.SaveAsync(file, _currentUser.UserId, ct);

        var link = new HrOrderFile
        {
            OrderId = order.Id,
            FileId = stored.Id,
            UploadedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.HrOrderFiles.Add(link);
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            link.Id,
            fileId = stored.Id,
            fileName = stored.OriginalFileName,
            sizeBytes = stored.SizeBytes,
            uploadedAt = link.CreatedAt,
        });
    }

    /// <summary>Сканы, приложенные к приказу.</summary>
    [HttpGet("{id:int}/files")]
    [RequirePermission(PermissionCode.ViewHrOrders)]
    public async Task<IActionResult> Files(int id, CancellationToken ct)
    {
        if (!await _db.HrOrders.AnyAsync(o => o.Id == id, ct)) return NotFound();

        return Ok(await _db.HrOrderFiles.AsNoTracking()
            .Where(f => f.OrderId == id)
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                fileId = f.FileId,
                fileName = f.File!.OriginalFileName,
                sizeBytes = f.File.SizeBytes,
                uploadedAt = f.CreatedAt,
                uploadedBy = f.UploadedByUserId,
            })
            .ToListAsync(ct));
    }

    /// <summary>
    /// Убрать скан из приказа.
    ///
    /// Сам файл в хранилище остаётся: он мог быть приложен и в другом месте, а
    /// удаление привязки — исправление ошибки вложения, не уничтожение документа.
    /// </summary>
    [HttpDelete("{id:int}/files/{fileLinkId:int}")]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> RemoveFile(int id, int fileLinkId, CancellationToken ct)
    {
        var link = await _db.HrOrderFiles
            .FirstOrDefaultAsync(f => f.Id == fileLinkId && f.OrderId == id, ct);

        if (link is null) return NotFound();

        _db.HrOrderFiles.Remove(link);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Приказы по конкретному сотруднику — его кадровая история.</summary>
    /// <summary>
    /// Завести лист ознакомления по приказу.
    ///
    /// Приказ по личному составу знакомят под роспись — без этого перевод,
    /// взыскание или изменение оклада остаются на бумаге. Поле листа у приказа
    /// было, а завести сам лист было нечем: он умел ссылаться только на документ
    /// единой карточки, на которой приказ не лежит.
    ///
    /// Участники по умолчанию — те, кого приказ касается.
    /// </summary>
    [HttpPost("{id:int}/acknowledgement")]
    [RequirePermission(PermissionCode.ManageHrOrders)]
    public async Task<IActionResult> CreateAcknowledgement(
        int id, [FromBody] HrAcknowledgementRequest request, CancellationToken ct)
    {
        var order = await _db.Set<Models.HrOrder>()
            .Include(o => o.Employees)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null) return NotFound(new {message = "Приказ не найден"});

        if (order.Status != Models.HrOrderStatus.Signed)
            return Conflict(new {message = "Знакомят с подписанным приказом"});

        var targets = request.Targets ?? new Documents.Services.AcknowledgementTargets();

        if (targets.UserIds.Count == 0 && targets.OrgUnitIds.Count == 0 && targets.UserGroupIds.Count == 0)
            targets.UserIds = order.Employees.Select(e => e.UserId).Distinct().ToList();

        try
        {
            var sheet = await _acknowledgements.CreateAsync(new Documents.Services.CreateSheetRequest
            {
                HrOrderId = order.Id,
                Instruction = request.Instruction ?? $"Ознакомьтесь с приказом № {order.RegNumber}",
                DueDate = request.DueDate,

                // Подписывать нечего: приказ вне единой карточки, и ознакомление
                // фиксируется отметкой — кто и когда её поставил.
                RequireSignature = false,
                Targets = targets,
            }, _currentUser.UserId, ct);

            order.AcknowledgementSheetId = sheet.Id;
            await _db.SaveChangesAsync(ct);

            return Ok(new {sheetId = sheet.Id});
        }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }

    [HttpGet("by-employee/{userId:int}")]
    public async Task<IActionResult> ByEmployee(int userId, CancellationToken ct)
    {
        // Свою историю сотрудник видит без особого права: это сведения о нём самом.
        if (userId != _currentUser.UserId
            && !_currentUser.HasPermission(PermissionCode.ViewHrOrders))
        {
            return Forbid();
        }

        var rows = await _db.HrOrders.AsNoTracking()
            .Where(o => o.Status == HrOrderStatus.Signed && o.Employees.Any(e => e.UserId == userId))
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.Id, o.RegNumber, o.OrderDate, o.EffectiveFrom, o.EffectiveTo, o.Title,
                Kind = o.Kind.ToString(),
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    private async Task FillEmployeesAsync(
        HrOrder order, List<HrOrderEmployeeRequest> requested, CancellationToken ct)
    {
        var ids = requested.Select(e => e.UserId).Distinct().ToList();

        var people = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                Unit = u.OrgUnit == null ? null : u.OrgUnit.TitleRu,
            })
            .ToDictionaryAsync(u => u.Id, ct);

        foreach (var item in requested.DistinctBy(e => e.UserId))
        {
            people.TryGetValue(item.UserId, out var person);

            order.Employees.Add(new HrOrderEmployee
            {
                UserId = item.UserId,
                // Снимок на момент издания: фамилия и должность сотрудника меняются,
                // а приказ должен читаться так, как он был издан.
                FullNameSnapshot = person?.FullName,
                PositionSnapshot = person?.Position,
                UnitSnapshot = person?.Unit,
                FieldValues = item.Fields?.ToString(),
            });
        }
    }

    private async Task<string> NextNumberAsync(int year, CancellationToken ct)
    {
        var used = await _db.HrOrders
            .Where(o => o.Year == year && o.RegNumber != null)
            .Select(o => o.RegNumber!)
            .ToListAsync(ct);

        var max = used
            .Select(n =>
            {
                var digits = new string(n.TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(digits, out var v) ? v : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        // «12-лс» — приказы по личному составу нумеруются отдельно от приказов
        // по основной деятельности, и индекс это показывает.
        return $"{max + 1}-лс";
    }

    private static string? Validate(HrOrderSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Укажите заголовок приказа.";

        if (request.Employees.Count == 0)
            return "Укажите хотя бы одного сотрудника.";

        if (request.EffectiveTo is { } to && request.EffectiveFrom is { } from && to < from)
            return "Дата окончания раньше даты начала.";

        return null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
