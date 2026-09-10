using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Meetings.Services;

/// <summary>Записка, ожидающая отбора в повестку.</summary>
/// <summary>Откуда вопрос пришёл к секретарю.</summary>
public enum AgendaCandidateKind
{
    /// <summary>Служебная записка, вынесенная адресатом на орган.</summary>
    Sz = 1,

    /// <summary>Заявка на закупку, расход по которой утверждает орган.</summary>
    Procurement = 2,
}

public class AgendaCandidateDto
{
    /// <summary>
    /// Вид источника. Секретарю видно, что перед ним: у записки и у заявки на
    /// закупку разные карточки и разные приложения, и открывать их надо в разных
    /// разделах.
    /// </summary>
    public AgendaCandidateKind Kind { get; set; } = AgendaCandidateKind.Sz;

    /// <summary>Заявка на закупку — заполнено, когда вопрос пришёл из закупок.</summary>
    public int? ProcurementRequestId { get; set; }

    /// <summary>Сумма расхода, который выносится на утверждение.</summary>
    public decimal? Amount { get; set; }

    public int SzId { get; set; }
    public int DocumentId { get; set; }
    public string? Number { get; set; }
    public DateOnly? RegisteredOn { get; set; }

    /// <summary>Тема записки — то, как она названа в реестре.</summary>
    public string? Subject { get; set; }

    /// <summary>Формулировка вопроса, предложенная автором. Пусто — секретарь берёт тему.</summary>
    public string? ProposedQuestion { get; set; }

    public string? AuthorName { get; set; }
    public string? AuthorUnit { get; set; }
    public string? SignerName { get; set; }
    public string Status { get; set; } = "";

    public DateTime? RequestedAt { get; set; }
    public string? RequestedBy { get; set; }

    /// <summary>Сколько файлов у записки — секретарю видно, есть ли что раздавать участникам.</summary>
    public int FileCount { get; set; }
}

public interface IAgendaCandidateService
{
    Task<List<AgendaCandidateDto>> ListAsync(MeetingBody body, CancellationToken ct = default);

    /// <summary>Включить в повестку заявку на закупку, утверждаемую органом.</summary>
    Task<AgendaItem> TakeProcurementIntoAgendaAsync(
        int meetingId, int requestId, string? question, int? order, int currentUserId,
        CancellationToken ct = default);

    Task<AgendaItem> TakeIntoAgendaAsync(
        int meetingId, int szId, string? question, int? order, int currentUserId, CancellationToken ct = default);

    Task<int> DeclineAsync(int szId, int currentUserId, CancellationToken ct = default);
}

/// <summary>
/// Отбор служебных записок в повестку заседания.
///
/// Записка с отметкой «вынести на орган» не попадает в повестку сама. Она встаёт в
/// очередь к секретарю этого органа, и он решает — включать, на какое заседание и
/// под какой формулировкой. Иначе повестку Правления определял бы любой сотрудник,
/// поставивший галочку.
/// </summary>
public class AgendaCandidateService : IAgendaCandidateService
{
    /// <summary>
    /// Статусы, в которых записка годится на заседание. Черновик и «на регистрации»
    /// не годятся: у них нет номера, а вопрос повестки без номера записки нечем
    /// подтвердить. Отклонённые и отозванные — тем более.
    /// </summary>
    private static readonly string[] EligibleStatuses =
    [
        SzStatus.Registered,
        SzStatus.OnAddresseeDecision,
        SzStatus.OnExecution,
        SzStatus.Executed,
        // Записку, помеченную «вынести на орган» (SubmitToBodyAsync → OnBoardReview),
        // секретарь ещё должен взять в повестку. Без этого статуса она выпадала из
        // кандидатов и в повестку попасть не могла — тупик.
        SzStatus.OnBoardReview,
    ];

    private readonly DelosferaDbContext _db;
    private readonly Documents.Services.IDocumentService _documents;

    public AgendaCandidateService(DelosferaDbContext db, Documents.Services.IDocumentService documents)
    {
        _db = db;
        _documents = documents;
    }

    public async Task<List<AgendaCandidateDto>> ListAsync(MeetingBody body, CancellationToken ct = default)
    {
        var записки = await ЗапискиАsync(body, ct);
        var заявки = await ЗаявкиНаЗакупкуАsync(body, ct);

        // В один список и по времени обращения: секретарю неважно, из какого
        // раздела вопрос, — важно, что он ждёт заседания и с какого числа.
        return записки.Concat(заявки).OrderBy(c => c.RequestedAt).ToList();
    }

    /// <summary>
    /// Заявки на закупку, расход по которым утверждает этот орган.
    ///
    /// Заявка не просит вынести себя на заседание — за неё это делает Матрица
    /// полномочий, определившая орган утверждения по сумме. Поэтому кандидатом
    /// она становится, когда маршрут дошёл до этапа ожидания решения органа.
    ///
    /// Совет директоров и общее собрание акционеров сюда не попадают: заседания
    /// этих органов система не ведёт, и делать вид, что попадают, значило бы
    /// прятать заявку в списке, который никто не разбирает.
    /// </summary>
    private async Task<List<AgendaCandidateDto>> ЗаявкиНаЗакупкуАsync(
        MeetingBody body, CancellationToken ct)
    {
        if (body != MeetingBody.Board) return [];

        var взятые = _db.AgendaItems
            .Where(a => a.SourceProcurementRequestId != null)
            .Select(a => a.SourceProcurementRequestId!.Value);

        return await _db.ProcurementRequests
            .AsNoTracking()
            .Where(r => r.ApprovalAuthority == ApprovalAuthority.Board && !взятые.Contains(r.Id))
            .Where(r => _db.RouteInstances
                .Any(i => i.DocumentId == r.DocumentId
                          && i.Steps.Any(st => st.Kind == StepKind.Board
                                               && st.Participants.Any(pt => pt.State == ParticipantState.Active))))
            .Select(r => new AgendaCandidateDto
            {
                Kind = AgendaCandidateKind.Procurement,
                ProcurementRequestId = r.Id,
                DocumentId = r.DocumentId,
                Number = r.Document!.RegNumber,
                Subject = r.Subject,
                Amount = r.Amount,
                ProposedQuestion = r.Subject,
                AuthorName = r.Document.Author == null ? null : r.Document.Author.FullName,
                AuthorUnit = r.InitiatorUnit == null ? null : r.InitiatorUnit.TitleRu,
                Status = r.Document.StatusCode,
                RequestedAt = r.UpdatedAt,
                FileCount = r.Document.Attachments.Count,
            })
            .ToListAsync(ct);
    }

    private async Task<List<AgendaCandidateDto>> ЗапискиАsync(MeetingBody body, CancellationToken ct)
    {
        // Уже включённые в повестку из отбора уходят: вопрос заведён, работа секретаря
        // по этой записке сделана.
        var taken = _db.AgendaItems
            .Where(a => a.SourceSzId != null)
            .Select(a => a.SourceSzId!.Value);

        return await _db.SzDocuments
            .AsNoTracking()
            .Where(s => s.SubmitToBody == body && !taken.Contains(s.Id))
            .Where(s => s.Document != null && EligibleStatuses.Contains(s.Document.StatusCode))
            .OrderBy(s => s.SubmitToBodyRequestedAt)
            .Select(s => new AgendaCandidateDto
            {
                SzId = s.Id,
                DocumentId = s.DocumentId,
                Number = s.Document!.RegNumber,
                RegisteredOn = s.RegisteredOn,
                Subject = s.Document.Title,
                ProposedQuestion = s.SubmitToBodyQuestion,
                AuthorName = s.Document.Author == null ? null : s.Document.Author.FullName,
                AuthorUnit = s.AuthorUnit == null ? null : s.AuthorUnit.TitleRu,
                SignerName = s.SignerUser == null ? null : s.SignerUser.FullName,
                Status = s.Document.StatusCode,
                RequestedAt = s.SubmitToBodyRequestedAt,
                RequestedBy = s.SubmitToBodyRequestedByUser == null
                    ? null
                    : s.SubmitToBodyRequestedByUser.FullName,
                FileCount = s.Document.Attachments.Count,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Включить в повестку заявку на закупку, расход по которой утверждает орган.
    ///
    /// Как и записку, её включает секретарь: очередь к органу он разбирает сам.
    /// Отличие в основании — заявка не помечена вручную, орган ей определила
    /// Матрица полномочий по сумме расхода.
    /// </summary>
    public async Task<AgendaItem> TakeProcurementIntoAgendaAsync(
        int meetingId, int requestId, string? question, int? order, int currentUserId,
        CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
            ?? throw new InvalidOperationException("Заседание не найдено.");

        var request = await _db.ProcurementRequests
            .Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException("Заявка на закупку не найдена.");

        if (request.ApprovalAuthority != ApprovalAuthority.Board)
            throw new InvalidOperationException(
                "Расход по этой заявке Правление не утверждает — на заседание она не выносится.");

        if (meeting.Body != MeetingBody.Board)
            throw new InvalidOperationException(
                "Заявка на закупку выносится на Правление — включить её в заседание другого органа нельзя.");

        if (await _db.AgendaItems.AnyAsync(a => a.SourceProcurementRequestId == requestId, ct))
            throw new InvalidOperationException("Заявка уже включена в повестку.");

        var now = DateTime.UtcNow;

        var nextOrder = order ?? await _db.AgendaItems
            .Where(a => a.MeetingId == meetingId)
            .Select(a => (int?)a.Order)
            .MaxAsync(ct) + 1 ?? 1;

        var item = new AgendaItem
        {
            MeetingId = meetingId,
            Order = nextOrder,
            Topic = Pick(question, request.Subject)
                    ?? $"Заявка на закупку № {request.Document?.RegNumber}",
            SourceProcurementRequestId = requestId,

            // Докладчик — инициатор закупки: он обосновывает расход перед органом.
            SpeakerUserId = request.Document?.AuthorId,
            SpeakerUnitId = request.InitiatorUnitId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.AgendaItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return item;
    }

    public async Task<AgendaItem> TakeIntoAgendaAsync(
        int meetingId, int szId, string? question, int? order, int currentUserId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
            ?? throw new InvalidOperationException("Заседание не найдено.");

        var sz = await _db.SzDocuments
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == szId, ct)
            ?? throw new InvalidOperationException("Записка не найдена.");

        if (sz.SubmitToBody is null)
            throw new InvalidOperationException("У записки нет отметки о вынесении на коллегиальный орган.");

        // Секретарь Правления не забирает записку, помеченную на кредитный комитет:
        // отметка адресована конкретному органу, и чужую очередь разбирать нельзя.
        if (sz.SubmitToBody != meeting.Body)
            throw new InvalidOperationException(
                "Записка помечена на другой орган — включить её в это заседание нельзя.");

        if (sz.Document is null || !EligibleStatuses.Contains(sz.Document.StatusCode))
            throw new InvalidOperationException(
                "Записку можно вынести на заседание только после регистрации.");

        var already = await _db.AgendaItems.AnyAsync(a => a.SourceSzId == szId, ct);
        if (already)
            throw new InvalidOperationException("Записка уже включена в повестку.");

        var now = DateTime.UtcNow;

        var nextOrder = order ?? await _db.AgendaItems
            .Where(a => a.MeetingId == meetingId)
            .Select(a => (int?)a.Order)
            .MaxAsync(ct) + 1 ?? 1;

        var topic = Pick(question, sz.SubmitToBodyQuestion, sz.Document.Title)
                    ?? $"Вопрос по служебной записке № {sz.Document.RegNumber}";

        var item = new AgendaItem
        {
            MeetingId = meetingId,
            Order = nextOrder,
            Topic = topic,
            SourceSzId = szId,
            // Докладчик — подписант записки, а если его нет, автор. Именно они
            // отвечают за вопрос перед органом.
            SpeakerUserId = sz.SignerUserId ?? sz.Document.AuthorId,
            SpeakerUnitId = sz.AuthorUnitId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.AgendaItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return item;
    }

    /// <summary>
    /// Секретарь отклоняет заявку: отметка снимается, записка уходит из очереди.
    /// Возвращает идентификатор записки.
    /// </summary>
    public async Task<int> DeclineAsync(int szId, int currentUserId, CancellationToken ct = default)
    {
        var sz = await _db.SzDocuments.Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == szId, ct)
            ?? throw new InvalidOperationException("Записка не найдена.");

        var already = await _db.AgendaItems.AnyAsync(a => a.SourceSzId == szId, ct);
        if (already)
            throw new InvalidOperationException(
                "Записка уже включена в повестку — снимите вопрос с повестки заседания.");

        sz.SubmitToBody = null;
        sz.SubmitToBodyQuestion = null;
        sz.SubmitToBodyRequestedAt = null;
        sz.SubmitToBodyRequestedByUserId = null;

        // Отклонённая секретарём записка возвращается на исполнение, а не остаётся
        // висеть «на рассмотрении органа» без отметки: иначе она застревает в
        // OnBoardReview и не доходит ни до исполнения, ни до архива.
        if (sz.Document is {StatusCode: SzStatus.OnBoardReview})
            await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.OnExecution, currentUserId);

        await _db.SaveChangesAsync(ct);
        return szId;
    }

    private static string? Pick(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
