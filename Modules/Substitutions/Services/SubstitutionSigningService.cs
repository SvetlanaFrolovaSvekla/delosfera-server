using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Signing.Models.InfocomEcp;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Substitutions.Models;
using ActingFor = delosfera_server.Modules.Users.Services.ActingFor;

namespace delosfera_server.Modules.Substitutions.Services;

// ── DTO ─────────────────────────────────────────────────────────────────────────

/// <summary>Состояние подписания документов заявки — для блока «Подписание» в карточке.</summary>
public sealed class SubstitutionSigningState
{
    /// <summary>Подписи принимаются: этап «На подписании» или (участникам маршрута) «На согласовании».</summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Заявка ещё на согласовании: подписать уже могут директор филиала и согласующие, которые
    /// сами подписанты (отсутствующий/замещающий), — на своём этапе; остальные — после маршрута.
    /// </summary>
    public bool IsApprovalStage { get; set; }

    /// <summary>Все подписи действующих версий собраны (места без подписанта не учитываются).</summary>
    public bool AllSigned { get; set; }

    /// <summary>Тестовая имитация ЭЦП разрешена сервером (EcpSigning:AllowSubstitutionTestEcp).</summary>
    public bool AllowTestEcp { get; set; }

    public List<SubstitutionSigningDocumentDto> Documents { get; set; } = [];
}

public sealed class SubstitutionSigningDocumentDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public bool IsCurrent { get; set; }
    public string FileName { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<SubstitutionSignerSlotDto> Slots { get; set; } = [];
}

public sealed class SubstitutionSignerSlotDto
{
    public string Role { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public int? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Текущий пользователь может поставить эту подпись сейчас (сам или за директора по замещению).</summary>
    public bool CanSign { get; set; }

    public SubstitutionSignatureDto? Signature { get; set; }

    /// <summary>
    /// Подпись того же подписанта под прежней версией документа — неактуальная: документ изменили
    /// после подписи (как неактуальная версия у СЗ и ВНД). Есть только когда под действующей
    /// версией подписи ещё нет.
    /// </summary>
    public SubstitutionSignatureDto? StaleSignature { get; set; }

    /// <summary>
    /// Новой подписи нет, а прежняя версия была подписана — на листе подписей ЭЦП место
    /// отмечается автосогласованием (пока подписант не подпишет новую версию).
    /// </summary>
    public bool AutoApproved { get; set; }
}

public sealed class SubstitutionSignatureDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? OnBehalfOfName { get; set; }
    public string? PersonFio { get; set; }
    public string? CertIssuerCommonName { get; set; }
    public string? CertValidNotAfter { get; set; }
    public string Hash { get; set; } = "";
    public DateTime SignedAt { get; set; }
    public bool Verified { get; set; }
    public bool IsTest { get; set; }

    /// <summary>Версия документа, под которой стоит подпись.</summary>
    public int DocumentVersion { get; set; }

    /// <summary>Место подписи в ЭЦП-версии (после абзаца N); null — в конце текста.</summary>
    public int? PlacementAnchor { get; set; }
    public int? PlacementIndentMm { get; set; }
}

/// <summary>
/// Место подписи, выбранное в окне подписания: после абзаца <see cref="Anchor"/> текста документа
/// (закладка dsa_N в макете) с отступом <see cref="IndentMm"/> от левого поля.
/// </summary>
public sealed class SubstitutionSignPlacement
{
    public int Anchor { get; set; }
    public int IndentMm { get; set; }
}

public sealed class SubstitutionSignTarget
{
    public int DocumentId { get; set; }
    public SubstitutionSignerRole Role { get; set; }

    /// <summary>Место подписи (для тестовой подписи); в хэш не входит.</summary>
    public SubstitutionSignPlacement? Placement { get; set; }
}

public sealed class SubstitutionEcpSignRequest
{
    public int DocumentId { get; set; }
    public SubstitutionSignerRole Role { get; set; }

    /// <summary>CMS-подпись хэша из 2.5, base64.</summary>
    public string? SignatureBase64 { get; set; }

    /// <summary>Хэш, который подписывали (backend пересчитает и сверит).</summary>
    public string? Hash { get; set; }

    /// <summary>ПИН, введённый при получении токена (2.3) — сверяется с ПИН из 2.6.</summary>
    public string? PersonIdnp { get; set; }

    /// <summary>Токен Инфокома — только для best-effort 2.4, НЕ сохраняется.</summary>
    public string? CertToken { get; set; }

    /// <summary>timestamp из ответа 2.5 (Unix ms).</summary>
    public long? ProviderTimestampMs { get; set; }

    /// <summary>Место подписи, выбранное в окне подписания; null — в конце текста. В хэш не входит.</summary>
    public SubstitutionSignPlacement? Placement { get; set; }
}

public interface ISubstitutionSigningService
{
    Task<SubstitutionSigningState> GetStateAsync(int requestId, int userId, CancellationToken ct = default);

    /// <summary>Хэш подписываемых данных (2.5): заявка, документ, версия, роль, SHA-256 файла.</summary>
    Task<string> GetHashAsync(int requestId, SubstitutionSignTarget target, int userId, CancellationToken ct = default);

    /// <summary>Полная проверка ЭЦП (как у СЗ/ВНД) и запись в журнал. Любое несоответствие —
    /// <see cref="ResolutionSignatureException"/>, подпись не сохраняется.</summary>
    Task SignAsync(int requestId, SubstitutionEcpSignRequest request, int userId, string? ip, string? userAgent,
        CancellationToken ct = default);

    /// <summary>ТЕСТ (EcpSigning:AllowSubstitutionTestEcp): имитация подписи без ГП «Инфоком». Включённость
    /// режима проверяет контроллер.</summary>
    Task SignTestAsync(int requestId, SubstitutionSignTarget target, int userId, string? ip, string? userAgent,
        CancellationToken ct = default);

    /// <summary>Файл документа: без подписей — зафиксированный файл как есть; с подписями — он же
    /// с листом подписей ЭЦП в конце.</summary>
    Task<(byte[] Bytes, string FileName)> DownloadAsync(int requestId, int documentId, bool signed,
        CancellationToken ct = default);

    /// <summary>Действующая версия документа данного вида (как DownloadAsync); null — документы ещё
    /// не зафиксированы (черновик), печатная форма тогда собирается по текущим данным.</summary>
    Task<(byte[] Bytes, string FileName)?> DownloadCurrentAsync(int requestId, SubstitutionDocumentKind kind, bool signed,
        CancellationToken ct = default);

    /// <summary>Макет ЭЦП-версии для окна выбора места подписи: уже поставленные подписи на своих
    /// местах и закладки-якоря dsa_N на абзацах (без серых мест и листа подписей).</summary>
    Task<(byte[] Bytes, string FileName)> LayoutAsync(int requestId, int documentId, CancellationToken ct = default);
}

/// <summary>
/// ЭЦП ГП «Инфоком» под приказом и договором заявки на замещение (этап «На подписании»).
/// Порядок проверки — тот же, что у резолюций СЗ (SzResolutionSignatureService):
///  1. подпись/хэш/ПИН есть, ПИН — 14 цифр;
///  2. хэш пересчитывается из БД (зафиксированный файл, версия, роль) и сверяется за постоянное время;
///  3. эту подпись ещё не предъявляли (журналы ВНД, СЗ и заявок);
///  4. подпись свежая — не старше 5 минут;
///  5. 2.6 check-sign с ПЕРЕСЧИТАННЫМ хэшем; провайдер недоступен — отказ;
///  6. ПИН владельца подписи совпадает с введённым и с ПИН, к которому учётка уже привязана;
///  7. best-effort 2.4 — сведения о сертификате.
/// Подписывает подписант места (директор — также его замещающий по GEN-14; отсутствующий и
/// замещающий — только лично).
/// </summary>
public sealed class SubstitutionSigningService : ISubstitutionSigningService
{
    public const string HashSchemaVersion = "sub-doc-v1";
    private const int MaxSignatureBytes = 256 * 1024;
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";
    private const string TestPin = "00000000000000";
    private const string TestNote = "ТЕСТ: имитация ЭЦП, у ГП «Инфоком» подпись не ставилась и не проверялась";

    private static readonly JsonSerializerOptions ProviderJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly DelosferaDbContext _db;
    private readonly IInfocomEcpClient _infocom;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;
    private readonly INotificationService? _notifications;
    private readonly ILogger<SubstitutionSigningService> _logger;
    private readonly bool _allowTestEcp;

    public SubstitutionSigningService(
        DelosferaDbContext db, IInfocomEcpClient infocom, IAuditService audit, IBankClock clock,
        Microsoft.Extensions.Options.IOptions<EcpSigningOptions> ecp,
        ILogger<SubstitutionSigningService> logger, INotificationService? notifications = null)
    {
        _db = db;
        _infocom = infocom;
        _audit = audit;
        _clock = clock;
        _logger = logger;
        _notifications = notifications;
        _allowTestEcp = ecp.Value.AllowSubstitutionTestEcp;
    }

    // ── Состояние ───────────────────────────────────────────────────────────────

    public async Task<SubstitutionSigningState> GetStateAsync(int requestId, int userId, CancellationToken ct = default)
    {
        var request = await _db.SubstitutionRequests.AsNoTracking()
                          .Where(x => x.Id == requestId).Select(x => new { x.Status })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        var docs = await _db.SubstitutionDocuments.AsNoTracking()
            .Where(d => d.RequestId == requestId)
            .OrderByDescending(d => d.IsCurrent).ThenBy(d => d.Kind).ThenByDescending(d => d.Version)
            .Select(d => new
            {
                d.Id, d.Kind, d.Version, d.IsCurrent, d.FileName, d.ContentSha256, d.CreatedAt,
                d.DirectorUserId, d.AbsentUserId, d.SubstituteUserId,
            })
            .ToListAsync(ct);
        var docIds = docs.Select(d => d.Id).ToList();
        var signatures = await _db.SubstitutionSignatures.AsNoTracking()
            .Where(s => docIds.Contains(s.DocumentId))
            .ToListAsync(ct);

        var userIds = docs.SelectMany(d => new[] { d.DirectorUserId, d.AbsentUserId, d.SubstituteUserId })
            .Where(x => x is not null).Select(x => x!.Value)
            .Concat(signatures.Select(s => s.UserId))
            .Concat(signatures.Where(s => s.OnBehalfOfUserId != null).Select(s => s.OnBehalfOfUserId!.Value))
            .Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var onExecution = request.Status == SubstitutionStatus.OnExecution;
        var onApproval = request.Status == SubstitutionStatus.OnApproval;
        // На согласовании подписывают те, чей этап уже наступил (активен или пройден): директор
        // филиала — сразу на своём этапе, Опер. управление и УЧР — если они же отсутствующий
        // или замещающий.
        var approvalSigners = onApproval ? await ApprovalStageSignersAsync(requestId, ct) : [];
        var open = onExecution || (onApproval && docs.Count > 0);
        var actingFor = open ? await ActingFor.SelfAndAbsentAsync(_db, userId, _clock.Today) : [];
        var docVersion = docs.ToDictionary(d => d.Id, d => (d.Kind, d.Version));

        var state = new SubstitutionSigningState
        {
            IsOpen = open,
            IsApprovalStage = onApproval && docs.Count > 0,
            AllowTestEcp = _allowTestEcp,
        };
        foreach (var d in docs)
        {
            var entity = new SubstitutionDocument
            {
                Kind = d.Kind, DirectorUserId = d.DirectorUserId, AbsentUserId = d.AbsentUserId,
                SubstituteUserId = d.SubstituteUserId, FileName = d.FileName, Content = [], ContentSha256 = d.ContentSha256,
            };
            var dto = new SubstitutionSigningDocumentDto
            {
                Id = d.Id,
                Kind = d.Kind.ToString(),
                Title = SubstitutionDocuments.KindTitle(d.Kind),
                Version = d.Version,
                IsCurrent = d.IsCurrent,
                FileName = d.FileName,
                ContentSha256 = d.ContentSha256,
                CreatedAt = d.CreatedAt,
            };
            foreach (var slot in SubstitutionDocuments.Slots(entity))
            {
                var sig = signatures.FirstOrDefault(s => s.DocumentId == d.Id && s.Role == slot.Role);
                // Неактуальная подпись: тот же подписант ставил её под прежней версией этого документа.
                var stale = sig is null && d.IsCurrent && slot.UserId is { } staleOwner
                    ? StaleSignatureOf(signatures, docVersion, d.Kind, d.Version, slot.Role, staleOwner)
                    : null;
                var stageAllows = onExecution || (onApproval && slot.UserId is { } so && approvalSigners.Contains(so));
                dto.Slots.Add(new SubstitutionSignerSlotDto
                {
                    Role = slot.Role.ToString(),
                    RoleTitle = SubstitutionDocuments.RoleTitle(d.Kind, slot.Role),
                    UserId = slot.UserId,
                    UserName = slot.UserId is { } u && names.TryGetValue(u, out var n) ? n : null,
                    CanSign = open && stageAllows && d.IsCurrent && sig is null && slot.UserId is { } owner
                              && (owner == userId || (slot.Role == SubstitutionSignerRole.Director && actingFor.Contains(owner))),
                    Signature = sig is null ? null : ToDto(sig, names, d.Version),
                    StaleSignature = stale is null ? null : ToDto(stale, names, docVersion[stale.DocumentId].Version),
                    AutoApproved = stale is not null,
                });
            }
            state.Documents.Add(dto);
        }

        var current = state.Documents.Where(d => d.IsCurrent).ToList();
        state.AllSigned = current.Count > 0
                          && current.All(d => d.Slots.Where(s => s.UserId is not null).All(s => s.Signature is not null));
        return state;
    }

    private static SubstitutionSignatureDto ToDto(SubstitutionSignature sig, IReadOnlyDictionary<int, string> names, int version) => new()
    {
        Id = sig.Id,
        UserId = sig.UserId,
        UserName = names.GetValueOrDefault(sig.UserId),
        OnBehalfOfName = sig.OnBehalfOfUserId is { } ob ? names.GetValueOrDefault(ob) : null,
        PersonFio = sig.PersonFio,
        CertIssuerCommonName = sig.CertIssuerCommonName,
        CertValidNotAfter = sig.CertValidNotAfter,
        Hash = sig.Hash,
        SignedAt = sig.SignedAt,
        Verified = sig.Verified,
        IsTest = !sig.Verified && sig.PersonIdnp == TestPin,
        DocumentVersion = version,
        PlacementAnchor = sig.PlacementAnchor,
        PlacementIndentMm = sig.PlacementIndentMm,
    };

    /// <summary>
    /// Последняя подпись того же подписанта (сам или за него — по замещению) в той же роли под
    /// прежней версией документа того же вида. Подпись прежнего директора при смене директора
    /// новому не засчитывается.
    /// </summary>
    private static SubstitutionSignature? StaleSignatureOf(
        IEnumerable<SubstitutionSignature> signatures, IReadOnlyDictionary<int, (SubstitutionDocumentKind Kind, int Version)> docs,
        SubstitutionDocumentKind kind, int version, SubstitutionSignerRole role, int owner) =>
        signatures
            .Where(s => s.Role == role && (s.OnBehalfOfUserId ?? s.UserId) == owner
                        && docs.TryGetValue(s.DocumentId, out var dv) && dv.Kind == kind && dv.Version < version)
            .OrderByDescending(s => docs[s.DocumentId].Version)
            .FirstOrDefault();

    /// <summary>
    /// Кто может подписывать, пока заявка на согласовании: согласующие этапов действующего круга,
    /// до которых очередь уже дошла (активный или пройденный этап). Место подписи — их собственное
    /// (директор — под приказом и договором; отсутствующий/замещающий — если согласующий он же).
    /// </summary>
    private async Task<HashSet<int>> ApprovalStageSignersAsync(int requestId, CancellationToken ct)
    {
        var steps = await _db.SubstitutionApprovals.AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .Select(a => new { a.Round, a.State, a.UserId })
            .ToListAsync(ct);
        if (steps.Count == 0) return [];
        var round = steps.Max(a => a.Round);
        return steps
            .Where(a => a.Round == round && a.State is SubstitutionApprovalState.Active or SubstitutionApprovalState.Approved)
            .Select(a => a.UserId)
            .ToHashSet();
    }

    // ── Хэш ─────────────────────────────────────────────────────────────────────

    public async Task<string> GetHashAsync(int requestId, SubstitutionSignTarget target, int userId, CancellationToken ct = default)
    {
        var (doc, _) = await LoadForSigningAsync(requestId, target, userId, ct);
        return ComputeHash(requestId, doc, target.Role);
    }

    /// <summary>SHA-256 канонической строки (hex upper-case): заявка, документ, вид, версия, роль и
    /// SHA-256 самого файла. Поменяй любой байт файла или подпиши не своё место — хэш другой.</summary>
    public static string ComputeHash(int requestId, SubstitutionDocument doc, SubstitutionSignerRole role)
    {
        var sb = new StringBuilder(HashSchemaVersion);
        sb.Append('\n').Append("request:").Append(requestId.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n').Append("document:").Append(doc.Id.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n').Append("kind:").Append(doc.Kind.ToString());
        sb.Append('\n').Append("version:").Append(doc.Version.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n').Append("role:").Append(role.ToString());
        sb.Append('\n').Append("content:").Append(doc.ContentSha256);
        return ResolutionSignatureHasher.Sha256Hex(sb.ToString());
    }

    // ── Подпись ЭЦП ─────────────────────────────────────────────────────────────

    public async Task SignAsync(int requestId, SubstitutionEcpSignRequest request, int userId, string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = new SubstitutionSignTarget { DocumentId = request.DocumentId, Role = request.Role };
        var (doc, owner) = await LoadForSigningAsync(requestId, target, userId, ct);

        // 1. Наличие и формат.
        if (string.IsNullOrWhiteSpace(request.SignatureBase64)
            || string.IsNullOrWhiteSpace(request.Hash)
            || string.IsNullOrWhiteSpace(request.PersonIdnp))
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.EcpRequired, "Для подписания документа требуется подпись ЭЦП");

        var claimedPin = request.PersonIdnp.Trim();
        if (!InfocomFieldRules.IsValidPin(claimedPin))
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.InvalidPin, InfocomFieldRules.PinError);

        var signatureBase64 = request.SignatureBase64.Trim();
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid, "Подпись ЭЦП повреждена (не base64)");
        }
        if (signatureBytes.Length == 0 || signatureBytes.Length > MaxSignatureBytes)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid, "Недопустимый размер подписи ЭЦП");

        // 2. Хэш — только из БД.
        var expectedHash = ComputeHash(requestId, doc, request.Role);
        if (!ResolutionSignatureHasher.HashesEqual(expectedHash, request.Hash))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.DocumentChanged,
                "Документ изменился после подписания — подпишите заново");

        // 3. Повторное предъявление — по журналам всех контуров.
        var signatureSha256 = ResolutionSignatureHasher.Sha256Hex(signatureBytes);
        if (await _db.SubstitutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct)
            || await _db.SzResolutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct)
            || await _db.VndResolutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.SignatureReused,
                "Эта подпись уже была использована — подпишите документ заново");

        // 4. Свежесть.
        var now = DateTime.UtcNow;
        var cmsSigningTime = TryReadCmsSigningTime(signatureBytes);
        var providerTimestamp = FromUnixMs(request.ProviderTimestampMs);
        var signingTime = cmsSigningTime ?? providerTimestamp
            ?? throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.StaleSignature,
                "Не удалось определить время подписи — подпишите документ заново");
        if (signingTime < now - ResolutionSignatureService.MaxSignatureAge)
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.StaleSignature,
                $"Подпись старше {ResolutionSignatureService.MaxSignatureAge.TotalMinutes:0} минут — подпишите документ заново");
        if (signingTime > now + ResolutionSignatureService.MaxClockSkew)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid, "Время подписи указано в будущем — подпись отклонена");

        // 5. 2.6 — с ПЕРЕСЧИТАННЫМ хэшем.
        var check = await CheckSignAsync(signatureBase64, expectedHash, ct)
                    ?? throw new ResolutionSignatureException(StatusCodes.Status503ServiceUnavailable,
                        ResolutionSignatureErrorCodes.ProviderUnavailable,
                        "Сервис проверки ЭЦП недоступен — подпись не сохранена, попробуйте позже");
        if (!check.Verified)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Подпись ЭЦП не прошла проверку у провайдера — подпись не сохранена");

        // 6. Личность.
        var signerPin = FormatPin(check.PersonIdnp)
                        ?? throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                            ResolutionSignatureErrorCodes.SignatureInvalid,
                            "Провайдер не вернул ПИН владельца подписи — подпись не сохранена");
        if (!string.Equals(signerPin, claimedPin, StringComparison.Ordinal))
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Подпись принадлежит другому лицу, а не владельцу введённого ПИН");
        await EnsurePinBindingAsync(userId, signerPin, ct);

        // 7. Сертификат — не обязателен.
        var cert = await TryGetCertInfoAsync(request.CertToken, signerPin, ct);
        var (anchor, indent) = Placement(doc, request.Placement);

        _db.SubstitutionSignatures.Add(new SubstitutionSignature
        {
            RequestId = requestId,
            DocumentId = doc.Id,
            Role = request.Role,
            UserId = userId,
            OnBehalfOfUserId = owner != userId ? owner : null,
            PersonIdnp = signerPin,
            PersonFio = Truncate(check.PersonFio, 300),
            OrganizationInn = Truncate(check.OrganizationInn, 20),
            OrganizationName = Truncate(check.OrganizationName, 500),
            CertKeyIdentifier = Truncate(cert?.KeyIdentifier, 200),
            CertCommonName = Truncate(cert?.CommonName, 300),
            CertValidNotBefore = Truncate(cert?.ValidNotBefore, 64),
            CertValidNotAfter = Truncate(cert?.ValidNotAfter, 64),
            CertIssuerCommonName = Truncate(cert?.CertIssuer?.CommonName, 300),
            CertIssuerOrganizationName = Truncate(cert?.CertIssuer?.OrganizationName, 500),
            HashSchemaVersion = HashSchemaVersion,
            Hash = expectedHash,
            SignatureBase64 = signatureBase64,
            SignatureSha256 = signatureSha256,
            SignedAt = now,
            ProviderTimestamp = providerTimestamp,
            CmsSigningTime = cmsSigningTime,
            VerifiedAt = DateTime.UtcNow,
            ProviderCheckTimestamp = FromUnixMs(check.Timestamp),
            Verified = true,
            IpAddress = Truncate(ip, 64),
            UserAgent = Truncate(userAgent, 512),
            PlacementAnchor = anchor,
            PlacementIndentMm = indent,
        });
        await _db.SaveChangesAsync(ct);
        await AfterSignedAsync(requestId, doc, request.Role, userId, owner, test: false, ct);
    }

    public async Task SignTestAsync(int requestId, SubstitutionSignTarget target, int userId, string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        var (doc, owner) = await LoadForSigningAsync(requestId, target, userId, ct);
        var hash = ComputeHash(requestId, doc, target.Role);
        var fake = RandomNumberGenerator.GetBytes(64);
        var now = DateTime.UtcNow;
        var name = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var (anchor, indent) = Placement(doc, target.Placement);

        _db.SubstitutionSignatures.Add(new SubstitutionSignature
        {
            RequestId = requestId,
            DocumentId = doc.Id,
            Role = target.Role,
            UserId = userId,
            OnBehalfOfUserId = owner != userId ? owner : null,
            PersonIdnp = TestPin,
            PersonFio = Truncate(name, 300),
            CertCommonName = Truncate(name, 300),
            CertValidNotAfter = now.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CertIssuerCommonName = "Тестовый УЦ (имитация)",
            HashSchemaVersion = HashSchemaVersion,
            Hash = hash,
            SignatureBase64 = Convert.ToBase64String(fake),
            SignatureSha256 = ResolutionSignatureHasher.Sha256Hex(fake),
            SignedAt = now,
            VerifiedAt = now,
            // Verified=false — признак тестовой имитации: к привязке ПИН отношения не имеет,
            // в подписанной версии документа печатается как «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП».
            Verified = false,
            VerificationError = TestNote,
            IpAddress = Truncate(ip, 64),
            UserAgent = Truncate(userAgent, 512),
            PlacementAnchor = anchor,
            PlacementIndentMm = indent,
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogWarning("Заявка на замещение {RequestId}: документ {DocumentId} ({Role}) подписан ТЕСТОВОЙ имитацией ЭЦП пользователем {UserId}",
            requestId, doc.Id, target.Role, userId);
        await AfterSignedAsync(requestId, doc, target.Role, userId, owner, test: true, ct);
    }

    // ── Файлы ───────────────────────────────────────────────────────────────────

    public async Task<(byte[] Bytes, string FileName)> DownloadAsync(int requestId, int documentId, bool signed,
        CancellationToken ct = default)
    {
        var doc = await _db.SubstitutionDocuments.AsNoTracking()
                      .FirstOrDefaultAsync(d => d.Id == documentId && d.RequestId == requestId, ct)
                  ?? throw new KeyNotFoundException("Документ заявки не найден");
        return await RenderAsync(doc, signed, ct);
    }

    public async Task<(byte[] Bytes, string FileName)?> DownloadCurrentAsync(int requestId, SubstitutionDocumentKind kind,
        bool signed, CancellationToken ct = default)
    {
        var doc = await _db.SubstitutionDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.RequestId == requestId && d.Kind == kind && d.IsCurrent, ct);
        return doc is null ? null : await RenderAsync(doc, signed, ct);
    }

    public async Task<(byte[] Bytes, string FileName)> LayoutAsync(int requestId, int documentId, CancellationToken ct = default)
    {
        var doc = await _db.SubstitutionDocuments.AsNoTracking()
                      .FirstOrDefaultAsync(d => d.Id == documentId && d.RequestId == requestId, ct)
                  ?? throw new KeyNotFoundException("Документ заявки не найден");
        return await RenderAsync(doc, signed: true, ct, layout: true);
    }

    /// <summary>
    /// Без подписей — зафиксированный файл байт в байт; с подписями — он же с листом подписей ЭЦП.
    /// Место, где под этой версией подписи нет, а подписант подписывал прежнюю версию (документ
    /// изменили после подписи), отмечается на листе автосогласованием.
    /// </summary>
    private async Task<(byte[] Bytes, string FileName)> RenderAsync(SubstitutionDocument doc, bool signed, CancellationToken ct,
        bool layout = false)
    {
        if (!signed) return (doc.Content, doc.FileName);

        var docVersion = await _db.SubstitutionDocuments.AsNoTracking()
            .Where(d => d.RequestId == doc.RequestId && d.Kind == doc.Kind)
            .ToDictionaryAsync(d => d.Id, d => (d.Kind, d.Version), ct);
        var docIds = docVersion.Keys.ToList();
        var all = await _db.SubstitutionSignatures.AsNoTracking()
            .Where(s => docIds.Contains(s.DocumentId)).ToListAsync(ct);
        var sigs = all.Where(s => s.DocumentId == doc.Id).ToList();

        var autoApproved = new List<SubstitutionSignature>();
        foreach (var slot in SubstitutionDocuments.Slots(doc))
        {
            if (slot.UserId is not { } owner || sigs.Any(s => s.Role == slot.Role)) continue;
            if (StaleSignatureOf(all, docVersion, doc.Kind, doc.Version, slot.Role, owner) is { } stale)
                autoApproved.Add(stale);
        }

        var slotUsers = SubstitutionDocuments.Slots(doc).Where(x => x.UserId is not null).Select(x => x.UserId!.Value);
        var ids = sigs.Concat(autoApproved).Select(s => s.UserId)
            .Concat(sigs.Concat(autoApproved).Where(s => s.OnBehalfOfUserId != null).Select(s => s.OnBehalfOfUserId!.Value))
            .Concat(slotUsers)
            .Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        SubstitutionStamp Stamp(SubstitutionSignature s, bool auto) => new(
            s.Role,
            s.PersonFio ?? names.GetValueOrDefault(s.UserId),
            s.OnBehalfOfUserId is { } ob ? names.GetValueOrDefault(ob) : null,
            s.SignedAt,
            s.CertIssuerCommonName,
            s.CertValidNotAfter,
            s.Hash,
            IsTest: !s.Verified && s.PersonIdnp == TestPin,
            AutoApproved: auto,
            SignedVersion: docVersion[s.DocumentId].Version,
            Anchor: s.PlacementAnchor,
            IndentMm: s.PlacementIndentMm);

        var stamps = sigs.Select(s => Stamp(s, false)).Concat(autoApproved.Select(s => Stamp(s, true))).ToList();

        // ФИО подписантов в строках подписей: сотрудник места, а если он не выбран из справочника —
        // ФИО из заявки.
        var request = await _db.SubstitutionRequests.AsNoTracking().Where(x => x.Id == doc.RequestId)
            .Select(x => new { x.AbsentName, x.SubstituteName }).FirstOrDefaultAsync(ct);
        string? NameOf(int? userId, string? fallback) =>
            userId is { } u && names.TryGetValue(u, out var n) ? n : fallback;
        var slotNames = new Dictionary<SubstitutionSignerRole, string?>
        {
            [SubstitutionSignerRole.Director] = NameOf(doc.DirectorUserId, null),
            [SubstitutionSignerRole.Absent] = NameOf(doc.AbsentUserId, request?.AbsentName),
            [SubstitutionSignerRole.Substitute] = NameOf(doc.SubstituteUserId, request?.SubstituteName),
        };

        var name = Path.GetFileNameWithoutExtension(doc.FileName) + (layout ? " (макет).docx" : " (с подписями ЭЦП).docx");
        return (SubstitutionDocuments.WithStamps(doc, stamps, _clock.Zone, slotNames, layout), name);
    }

    // ── Внутреннее ──────────────────────────────────────────────────────────────

    /// <summary>Место подписи в пределах документа; не выбрано или вне документа — в конце текста (null).</summary>
    private static (int? Anchor, int? IndentMm) Placement(SubstitutionDocument doc, SubstitutionSignPlacement? placement)
    {
        if (placement is null) return (null, null);
        int count;
        try { count = SubstitutionDocuments.AnchorCount(doc); }
        catch (Exception ex) when (ex is DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or InvalidDataException or IOException) { return (null, null); }
        var (anchor, indent) = SubstitutionDocuments.NormalizePlacement(placement.Anchor, placement.IndentMm, count);
        return (anchor, indent);
    }

    /// <summary>
    /// Документ и владелец места подписи, которое ставит текущий пользователь. Проверяет этап
    /// (только «На подписании»), действующую версию, что место ещё не подписано и что подписывает
    /// именно его владелец (директора — также его замещающий, GEN-14).
    /// </summary>
    private async Task<(SubstitutionDocument Doc, int Owner)> LoadForSigningAsync(
        int requestId, SubstitutionSignTarget target, int userId, CancellationToken ct)
    {
        var status = await _db.SubstitutionRequests.AsNoTracking()
                         .Where(x => x.Id == requestId).Select(x => (SubstitutionStatus?)x.Status)
                         .FirstOrDefaultAsync(ct)
                     ?? throw new KeyNotFoundException("Заявка на замещение не найдена");
        if (status is not (SubstitutionStatus.OnExecution or SubstitutionStatus.OnApproval))
            throw new InvalidOperationException("Подписывать документы можно только на согласовании или на подписании");

        var doc = await _db.SubstitutionDocuments.AsNoTracking()
                      .FirstOrDefaultAsync(d => d.Id == target.DocumentId && d.RequestId == requestId, ct)
                  ?? throw new KeyNotFoundException("Документ заявки не найден");
        if (!doc.IsCurrent)
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.DocumentChanged,
                "Документ изменён УЧР — подпишите новую версию");

        var slot = SubstitutionDocuments.Slots(doc).FirstOrDefault(s => s.Role == target.Role)
                   ?? throw new InvalidOperationException("Этот документ в таком качестве не подписывается");
        if (slot.UserId is not { } owner)
            throw new InvalidOperationException("Для этого места подписи не назначен сотрудник");

        // На согласовании подписывает только тот, чей этап маршрута уже наступил: директор филиала
        // на своём этапе, Опер. управление и УЧР — если они же отсутствующий или замещающий.
        if (status == SubstitutionStatus.OnApproval
            && !(await ApprovalStageSignersAsync(requestId, ct)).Contains(owner))
            throw new InvalidOperationException(
                "Пока заявка на согласовании, подписать могут только участники маршрута на своём этапе — " +
                "остальные подписи собираются после согласования");

        if (owner != userId)
        {
            var mayActFor = target.Role == SubstitutionSignerRole.Director
                            && await ActingFor.ActsForAsync(_db, userId, owner, _clock.Today);
            if (!mayActFor)
                throw new UnauthorizedAccessException(target.Role == SubstitutionSignerRole.Director
                    ? "Подписать за директора может только сам директор или его замещающий"
                    : "Ознакомиться и подписать может только сам сотрудник");
        }

        if (await _db.SubstitutionSignatures.AnyAsync(s => s.DocumentId == doc.Id && s.Role == target.Role, ct))
            throw new InvalidOperationException("Документ в этом качестве уже подписан");

        return (doc, owner);
    }

    /// <summary>Журнал, уведомление УЧР, когда собраны все подписи действующих версий.</summary>
    private async Task AfterSignedAsync(int requestId, SubstitutionDocument doc, SubstitutionSignerRole role, int userId,
        int owner, bool test, CancellationToken ct)
    {
        await _audit.LogAsync(SubstitutionService.AuditEntityType, requestId, test ? "SignedTest" : "Signed", userId,
            new
            {
                document = doc.Kind.ToString(),
                doc.Version,
                role = role.ToString(),
                onBehalfOf = owner != userId ? owner : (int?)null,
            });

        var state = await GetStateAsync(requestId, userId, ct);
        // «Все подписали — можно исполнять» — только на этапе подписания: на согласовании часть
        // подписей уже может стоять, но маршрут ещё не пройден.
        if (!state.AllSigned || state.IsApprovalStage || _notifications is null) return;

        var r = await _db.SubstitutionRequests.AsNoTracking().Where(x => x.Id == requestId)
            .Select(x => new { x.RegNumber, x.InitiatorUserId, x.Subject }).FirstAsync(ct);
        var hr = await _db.HrRoutingSettings.AsNoTracking()
            .Select(x => new { x.HeadOfficeHrUserId, x.BranchHrUserId }).FirstOrDefaultAsync(ct);
        var recipients = new[] { hr?.HeadOfficeHrUserId, hr?.BranchHrUserId, (int?)r.InitiatorUserId }
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = "Документы по заявке на замещение подписаны",
                BodyRu = $"Заявка на замещение {r.RegNumber ?? $"#{requestId}"}: приказ и договор подписаны всеми " +
                         $"подписантами — УЧР может отметить заявку исполненной. Тема: {r.Subject}",
                Category = NotificationCategory.Sz,
                EntityType = "SubstitutionRequest",
                EntityId = requestId,
                Url = $"/substitutions/{requestId}",
                UserIds = recipients,
            }, userId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Не удалось отправить уведомление о подписании заявки {RequestId}", requestId);
        }
    }

    /// <summary>Привязка ПИН ↔ учётка по журналам подписей ВНД, СЗ и заявок на замещение.</summary>
    private async Task EnsurePinBindingAsync(int userId, string signerPin, CancellationToken ct)
    {
        var boundToOtherPin =
            await _db.VndResolutionSignatures.AnyAsync(s => s.Verified && s.UserId == userId && s.PersonIdnp != signerPin, ct)
            || await _db.SzResolutionSignatures.AnyAsync(s => s.Verified && s.UserId == userId && s.PersonIdnp != signerPin, ct)
            || await _db.SubstitutionSignatures.AnyAsync(s => s.Verified && s.UserId == userId && s.PersonIdnp != signerPin, ct);
        if (boundToOtherPin)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Ваша учётная запись уже привязана к ЭЦП другого лица — подписывать можно только своей ЭЦП");

        var usedByOtherUser =
            await _db.VndResolutionSignatures.AnyAsync(s => s.Verified && s.PersonIdnp == signerPin && s.UserId != userId, ct)
            || await _db.SzResolutionSignatures.AnyAsync(s => s.Verified && s.PersonIdnp == signerPin && s.UserId != userId, ct)
            || await _db.SubstitutionSignatures.AnyAsync(s => s.Verified && s.PersonIdnp == signerPin && s.UserId != userId, ct);
        if (usedByOtherUser)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Эта ЭЦП уже использовалась другим пользователем системы — подпись отклонена");
    }

    private static bool IsInvalidSignatureError(InfocomEcpException ex) =>
        ex.StatusCode == 500
        && ex.Error.ErrorMessage?.Contains("Ошибка при проверке подписи", StringComparison.OrdinalIgnoreCase) == true;

    private async Task<InfocomCheckSignResult?> CheckSignAsync(string signatureBase64, string hash, CancellationToken ct)
    {
        try
        {
            var json = await _infocom.CheckSignAsync(
                new InfocomCheckSignRequest { SignBase64 = signatureBase64, Hash = hash }, ct);
            return json.Deserialize<InfocomCheckSignResult>(ProviderJson) ?? new InfocomCheckSignResult();
        }
        catch (InfocomEcpException ex) when (ex.StatusCode is 400 or 422 || IsInvalidSignatureError(ex))
        {
            _logger.LogWarning("Замещение 2.6 check-sign: провайдер отклонил подпись (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return new InfocomCheckSignResult { Verified = false };
        }
        catch (InfocomEcpException ex)
        {
            _logger.LogWarning("Замещение 2.6 check-sign: ошибка ecp-service/провайдера (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Замещение 2.6 check-sign: нет связи с delosfera-ecp-service");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Замещение 2.6 check-sign: таймаут delosfera-ecp-service");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Замещение 2.6 check-sign: неожиданный формат ответа");
            return null;
        }
    }

    private async Task<InfocomCertInfoResult?> TryGetCertInfoAsync(string? token, string signerPin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var json = await _infocom.GetCertInfoAsync(new InfocomGetCertInfoRequest { UserToken = token }, ct);
            var cert = json.Deserialize<InfocomCertInfoResult>(ProviderJson);
            if (cert is null) return null;
            return string.Equals(FormatPin(cert.PersonIdnp), signerPin, StringComparison.Ordinal) ? cert : null;
        }
        catch (Exception ex) when (ex is InfocomEcpException or HttpRequestException or JsonException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.LogInformation("Замещение 2.4 get-cert-info недоступен ({Error}) — сведения о сертификате не сохранены",
                ex.GetType().Name);
            return null;
        }
    }

    private static DateTime? TryReadCmsSigningTime(byte[] signature)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(signature);
            foreach (var signer in cms.SignerInfos)
            foreach (var attribute in signer.SignedAttributes)
            {
                if (attribute.Oid.Value != SigningTimeOid) continue;
                foreach (var value in attribute.Values)
                {
                    var signingTime = value as Pkcs9SigningTime ?? new Pkcs9SigningTime(value.RawData);
                    return signingTime.SigningTime.ToUniversalTime();
                }
            }
        }
        catch (CryptographicException)
        {
            // Не CMS или непривычная структура — подлинность проверяет 2.6.
        }
        return null;
    }

    private static DateTime? FromUnixMs(long? ms)
    {
        if (ms is not > 0) return null;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string? FormatPin(long? pin) =>
        pin is > 0 ? pin.Value.ToString("D14", CultureInfo.InvariantCulture) : null;

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
