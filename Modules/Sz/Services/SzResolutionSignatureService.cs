using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Models.InfocomEcp;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>Хэш резолюции для подписи ЭЦП (его подписывают в 2.5).</summary>
public sealed class SzResolutionHashResponse
{
    public required string Hash { get; init; }
    public required string SchemaVersion { get; init; }
}

/// <summary>Решение согласующего по записке вместе с подписью ЭЦП ГП «Инфоком».</summary>
public sealed class SzEcpResolveRequest
{
    public int ParticipantId { get; set; }
    public ResolutionType Decision { get; set; }
    public string? Comment { get; set; }

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
}

/// <summary>Что подписать сохранённой идентификацией (без окна ПИН/кода).</summary>
public sealed class SzStoredEcpSignRequest
{
    public int ParticipantId { get; set; }
    public ResolutionType Decision { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Сведения ЭЦП для штампа в листе согласования (по подписи контура).</summary>
public sealed record SzEcpStampInfo(
    string? PersonFio,
    string? OrganizationName,
    string? CertValidNotAfter,
    string? CertIssuerCommonName,
    DateTime SignedAt,
    string Hash,
    string SignatureSha256,
    string ContentDigest,
    bool IsTest = false);

public interface ISzResolutionSignatureService
{
    /// <summary>Хэш резолюции для подписи: решение и комментарий — из запроса, содержимое записки —
    /// из БД. Только для активного участника маршрута (или того, кто его замещает).</summary>
    Task<SzResolutionHashResponse> GetHashAsync(
        int szId, int participantId, ResolutionType decision, string? comment, int currentUserId,
        CancellationToken ct = default);

    /// <summary>Полная проверка подписи (хэш из БД, replay, свежесть, 2.6, личность подписанта), затем
    /// в одной транзакции: подпись контура + запись журнала + резолюция маршрута. Любое несоответствие —
    /// <see cref="ResolutionSignatureException"/>, резолюция не сохраняется (fail closed).</summary>
    Task ResolveWithEcpAsync(
        int szId, SzEcpResolveRequest request, int currentUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default);

    /// <summary>ТЕСТ (EcpSigning:AllowSzTestEcp): решение с имитацией ЭЦП — без ГП «Инфоком». Хэш строится
    /// так же, как для настоящей подписи, но сама «подпись» — случайные байты, в журнале Verified=false и
    /// пометка «ТЕСТ». Проверку включённости режима делает контроллер.</summary>
    Task ResolveWithTestEcpAsync(
        int szId, SzStoredEcpSignRequest request, int currentUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default);

    /// <summary>Журнал подписей ЭЦП записки — всем, кто видит записку.</summary>
    Task<List<SzResolutionSignatureResponse>> ListAsync(int szId, int currentUserId, CancellationToken ct = default);

    /// <summary>Повторная проверка подписи: компоненты хэша из текущего состояния записки и резолюции +
    /// 2.6 у провайдера по сохранённой подписи. Ничего не изменяет (журнал неизменяем).</summary>
    Task<ResolutionSignatureVerifyResponse> ReverifyAsync(
        int szId, int signatureId, int currentUserId, CancellationToken ct = default);
}

/// <summary>
/// ЭЦП ГП «Инфоком» на резолюциях согласующих служебной записки — тот же порядок проверки, что у ВНД
/// (ResolutionSignatureService):
///  1. подпись/хэш/ПИН присутствуют, ПИН — 14 цифр;
///  2. хэш пересчитывается из БД (решение + комментарий + содержимое записки) и сравнивается с
///     присланным за постоянное время;
///  3. эту же подпись ещё не предъявляли;
///  4. подпись свежая (signingTime из CMS, иначе timestamp 2.5) — не старше 5 минут;
///  5. 2.6 check-sign с ПЕРЕСЧИТАННЫМ хэшем; ГП недоступен — отказ;
///  6. ПИН владельца подписи из 2.6 совпадает с введённым и с ПИН, к которому учётка уже привязана
///     (по журналам подписей ВНД и СЗ — одна ЭЦП на одного пользователя delosfera);
///  7. best-effort 2.4 (сведения о сертификате) — только если ПИН в сертификате тот же.
/// </summary>
public sealed class SzResolutionSignatureService : ISzResolutionSignatureService
{
    private const int MaxCommentLength = 35000;
    private const int MaxSignatureBytes = 256 * 1024;
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    public const string EcpLevelTitle = "Квалифицированная электронная подпись (ЭЦП ГП «Инфоком»)";

    private static readonly JsonSerializerOptions ProviderJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly DelosferaDbContext _db;
    private readonly IInfocomEcpClient _infocom;
    private readonly IRouteEngine _routeEngine;
    private readonly ISubstitutionService _substitutions;
    private readonly ILogger<SzResolutionSignatureService> _logger;
    private readonly ICurrentUserService _currentUser;

    public SzResolutionSignatureService(
        DelosferaDbContext db,
        IInfocomEcpClient infocom,
        IRouteEngine routeEngine,
        ISubstitutionService substitutions,
        ILogger<SzResolutionSignatureService> logger,
        ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
        _db = db;
        _infocom = infocom;
        _routeEngine = routeEngine;
        _substitutions = substitutions;
        _logger = logger;
    }

    // ----------------------------------------------------------------------------------------
    // Хэш для подписи
    // ----------------------------------------------------------------------------------------

    public async Task<SzResolutionHashResponse> GetHashAsync(
        int szId, int participantId, ResolutionType decision, string? comment, int currentUserId,
        CancellationToken ct = default)
    {
        var (sz, _) = await LoadForDecisionAsync(szId, participantId, decision, comment, currentUserId, ct);
        var components = SzResolutionSignatureHasher.CreateComponents(
            decision, comment, await BuildContentDigestAsync(sz, ct), sz.DocumentId);

        return new SzResolutionHashResponse
        {
            Hash = SzResolutionSignatureHasher.Compute(components),
            SchemaVersion = SzResolutionSignatureHasher.SchemaVersion,
        };
    }

    // ----------------------------------------------------------------------------------------
    // Проверка подписи и резолюция
    // ----------------------------------------------------------------------------------------

    public async Task ResolveWithEcpAsync(
        int szId, SzEcpResolveRequest request, int currentUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (sz, participant) = await LoadForDecisionAsync(
            szId, request.ParticipantId, request.Decision, request.Comment, currentUserId, ct);

        // 1. Наличие и формат.
        if (string.IsNullOrWhiteSpace(request.SignatureBase64)
            || string.IsNullOrWhiteSpace(request.Hash)
            || string.IsNullOrWhiteSpace(request.PersonIdnp))
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.EcpRequired, "Для резолюции требуется подпись ЭЦП");

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

        // 2. Хэш — только из актуальных данных БД.
        var contentDigest = await BuildContentDigestAsync(sz, ct);
        var components = SzResolutionSignatureHasher.CreateComponents(
            request.Decision, request.Comment, contentDigest, sz.DocumentId);
        var expectedHash = SzResolutionSignatureHasher.Compute(components);
        if (!ResolutionSignatureHasher.HashesEqual(expectedHash, request.Hash))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.DocumentChanged,
                "Записка или комментарий изменились после подписания — подпишите заново");

        // 3. Replay — по журналам обоих контуров: подпись, предъявленная под ВНД, сюда тоже не годится.
        var signatureSha256 = ResolutionSignatureHasher.Sha256Hex(signatureBytes);
        if (await _db.SzResolutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct)
            || await _db.VndResolutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.SignatureReused,
                "Эта подпись уже была использована — подпишите резолюцию заново");

        // 4. Свежесть.
        var now = DateTime.UtcNow;
        var cmsSigningTime = TryReadCmsSigningTime(signatureBytes);
        var providerTimestamp = FromUnixMs(request.ProviderTimestampMs);
        var signingTime = cmsSigningTime ?? providerTimestamp;
        if (signingTime is null)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.StaleSignature,
                "Не удалось определить время подписи — подпишите резолюцию заново");
        if (signingTime.Value < now - ResolutionSignatureService.MaxSignatureAge)
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.StaleSignature,
                $"Подпись старше {ResolutionSignatureService.MaxSignatureAge.TotalMinutes:0} минут — подпишите резолюцию заново");
        if (signingTime.Value > now + ResolutionSignatureService.MaxClockSkew)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Время подписи указано в будущем — подпись отклонена");

        // 5. 2.6 — с ПЕРЕСЧИТАННЫМ хэшем.
        var check = await CheckSignAsync(signatureBase64, expectedHash, ct)
                    ?? throw new ResolutionSignatureException(StatusCodes.Status503ServiceUnavailable,
                        ResolutionSignatureErrorCodes.ProviderUnavailable,
                        "Сервис проверки ЭЦП недоступен — резолюция не сохранена, попробуйте позже");
        if (!check.Verified)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Подпись ЭЦП не прошла проверку у провайдера — резолюция не сохранена");

        // 6. Личность: ПИН из 2.6 — источник правды.
        var signerPin = FormatPin(check.PersonIdnp)
                        ?? throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                            ResolutionSignatureErrorCodes.SignatureInvalid,
                            "Провайдер не вернул ПИН владельца подписи — резолюция не сохранена");
        if (!string.Equals(signerPin, claimedPin, StringComparison.Ordinal))
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Подпись принадлежит другому лицу, а не владельцу введённого ПИН");

        await EnsurePinBindingAsync(currentUserId, signerPin, ct);

        // 7. Сертификат — не обязателен.
        var cert = await TryGetCertInfoAsync(request.CertToken, signerPin, ct);

        // Штамп для общей подписи контура: реквизиты на момент подписания.
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.FullName, Position = u.Position != null ? u.Position.TitleRu : null })
            .FirstOrDefaultAsync(ct);
        var stampMeta = JsonSerializer.Serialize(new
        {
            fullName = check.PersonFio ?? user?.FullName,
            position = user?.Position,
            levelTitle = EcpLevelTitle,
            ecp = true,
            organizationName = check.OrganizationName,
        });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var signature = new Signature
        {
            DocumentId = sz.DocumentId,
            ContentHash = contentDigest,
            UserId = currentUserId,
            Level = SignatureLevel.Qualified,
            At = now,
            StampMeta = stampMeta,
            Revoked = false,
        };
        _db.Signatures.Add(signature);
        await _db.SaveChangesAsync(ct);

        _db.SzResolutionSignatures.Add(new SzResolutionSignature
        {
            SzDocumentId = sz.Id,
            DocumentId = sz.DocumentId,
            RouteParticipantId = participant.Id,
            SignatureId = signature.Id,
            Decision = request.Decision,
            UserId = currentUserId,

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

            HashSchemaVersion = SzResolutionSignatureHasher.SchemaVersion,
            Hash = expectedHash,
            CommentText = components.Comment,
            CommentHash = ResolutionSignatureHasher.Sha256Hex(components.Comment),
            ContentDigest = contentDigest,
            SignatureBase64 = signatureBase64,
            SignatureSha256 = signatureSha256,

            SignedAt = now,
            ProviderTimestamp = providerTimestamp,
            CmsSigningTime = cmsSigningTime,
            VerifiedAt = DateTime.UtcNow,
            ProviderCheckTimestamp = FromUnixMs(check.Timestamp),
            Verified = true,
            VerificationError = null,

            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
        });
        await _db.SaveChangesAsync(ct);

        // Резолюция маршрута закрывается этой подписью — дальше всё как при обычном решении
        // (переход этапа, замечания, завершение маршрута и обработчик контура СЗ).
        await _routeEngine.ResolveAsync(participant.Id, request.Decision,
            string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            currentUserId, signature.Id);

        await tx.CommitAsync(ct);
    }

    // ----------------------------------------------------------------------------------------
    // Журнал и повторная проверка
    // ----------------------------------------------------------------------------------------

    public async Task<List<SzResolutionSignatureResponse>> ListAsync(
        int szId, int currentUserId, CancellationToken ct = default)
    {
        var sz = await LoadVisibleAsync(szId, currentUserId, ct);
        var currentDigest = await BuildContentDigestAsync(sz, ct);

        var rows = await _db.SzResolutionSignatures.AsNoTracking()
            .Where(s => s.SzDocumentId == szId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new
            {
                s.Id, s.RouteParticipantId,
                RouteInstanceId = s.RouteParticipant!.RouteStep!.RouteInstanceId,
                StepOrder = s.RouteParticipant.RouteStep.Order,
                s.Decision, s.UserId, UserFullName = s.User!.FullName,
                s.PersonFio, s.OrganizationName, s.CertCommonName, s.CertIssuerCommonName, s.CertValidNotAfter,
                s.HashSchemaVersion, s.Hash, s.SignatureSha256, s.ContentDigest,
                s.SignedAt, s.VerifiedAt, s.Verified, s.VerificationError,
            })
            .ToListAsync(ct);

        var currentRouteId = sz.Document?.CurrentRouteInstanceId;
        return rows.Select(r => new SzResolutionSignatureResponse
        {
            Id = r.Id,
            RouteParticipantId = r.RouteParticipantId,
            RouteInstanceId = r.RouteInstanceId,
            IsCurrentRoute = currentRouteId == r.RouteInstanceId,
            StepOrder = r.StepOrder,
            Decision = r.Decision,
            UserId = r.UserId,
            UserFullName = r.UserFullName,
            PersonFio = r.PersonFio,
            OrganizationName = r.OrganizationName,
            CertCommonName = r.CertCommonName,
            CertIssuerCommonName = r.CertIssuerCommonName,
            CertValidNotAfter = r.CertValidNotAfter,
            HashSchemaVersion = r.HashSchemaVersion,
            Hash = r.Hash,
            SignatureSha256 = r.SignatureSha256,
            MatchesCurrentContent = string.Equals(r.ContentDigest, currentDigest, StringComparison.OrdinalIgnoreCase),
            SignedAt = r.SignedAt,
            VerifiedAt = r.VerifiedAt,
            Verified = r.Verified,
            IsTest = !r.Verified && r.VerificationError == TestVerificationNote,
        }).ToList();
    }

    public async Task<ResolutionSignatureVerifyResponse> ReverifyAsync(
        int szId, int signatureId, int currentUserId, CancellationToken ct = default)
    {
        var sz = await LoadVisibleAsync(szId, currentUserId, ct);

        var entry = await _db.SzResolutionSignatures.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == signatureId && s.SzDocumentId == szId, ct)
                    ?? throw new KeyNotFoundException($"Подпись с id={signatureId} не найдена");

        var result = new ResolutionSignatureVerifyResponse { Status = "valid", Message = "", CheckedAt = DateTime.UtcNow };

        if (entry.HashSchemaVersion != SzResolutionSignatureHasher.SchemaVersion)
        {
            result.Status = "unsupported_schema";
            result.Message = $"Неизвестная версия схемы хэша \"{entry.HashSchemaVersion}\"";
            return result;
        }

        // Запись журнала сама с собой: хранимые компоненты должны давать хранимый хэш.
        var stored = new SzResolutionHashComponents(
            SzResolutionSignatureHasher.DecisionCode(entry.Decision), entry.CommentText, entry.ContentDigest,
            entry.DocumentId.ToString(CultureInfo.InvariantCulture));
        var journalConsistent =
            ResolutionSignatureHasher.HashesEqual(entry.Hash, SzResolutionSignatureHasher.Compute(stored))
            && ResolutionSignatureHasher.HashesEqual(entry.CommentHash, ResolutionSignatureHasher.Sha256Hex(entry.CommentText));

        // Текущее состояние: резолюция участника и содержимое записки.
        var resolution = await _db.Resolutions.AsNoTracking()
            .Where(r => r.RouteParticipantId == entry.RouteParticipantId)
            .Select(r => new { r.Type, r.Comment, r.SignatureId })
            .FirstOrDefaultAsync(ct);
        if (resolution is null || resolution.SignatureId != entry.SignatureId || resolution.Type != entry.Decision)
            result.ChangedComponents.Add("decision");
        else if (!ResolutionSignatureHasher.HashesEqual(entry.CommentHash,
                     ResolutionSignatureHasher.Sha256Hex(ResolutionSignatureHasher.NormalizeComment(resolution.Comment))))
            result.ChangedComponents.Add("comment");

        var liveDigest = await BuildContentDigestAsync(sz, ct);
        if (!string.Equals(liveDigest, entry.ContentDigest, StringComparison.OrdinalIgnoreCase))
            result.ChangedComponents.Add("content");

        var isTest = !entry.Verified && entry.VerificationError == TestVerificationNote;
        if (isTest)
        {
            result.SignatureVerified = false;
            result.SignerPersonFio = entry.PersonFio;
        }
        else
        {
            var check = await CheckSignAsync(entry.SignatureBase64, entry.Hash, ct);
            result.SignatureVerified = check is null
                ? null
                : check.Verified && string.Equals(FormatPin(check.PersonIdnp), entry.PersonIdnp, StringComparison.Ordinal);
            result.SignerPersonFio = check?.PersonFio ?? entry.PersonFio;
        }

        string Changed() => string.Join(", ", result.ChangedComponents.Select(c => c switch
        {
            "comment" => "текст резолюции",
            "content" => "текст или реквизиты записки (в т.ч. вложения)",
            "decision" => "решение по этапу",
            _ => c,
        }));

        if (!journalConsistent)
        {
            result.Status = "journal_tampered";
            result.Message = "Запись журнала подписей не согласована сама с собой — данные изменены в обход системы";
        }
        else if (isTest)
        {
            // Для клиента — как "document_changed"/"provider_unavailable": жёлтая плашка, не красная.
            result.Status = result.ChangedComponents.Count > 0 ? "document_changed" : "provider_unavailable";
            result.Message = "Тестовая имитация ЭЦП — у провайдера не проверяется"
                             + (result.ChangedComponents.Count > 0
                                 ? $"; после «подписания» изменено: {Changed()}"
                                 : "; записка и резолюция с момента «подписания» не менялись");
        }
        else if (result.SignatureVerified == false)
        {
            result.Status = "signature_invalid";
            result.Message = "Провайдер не подтвердил подпись или её владельца";
        }
        else if (result.ChangedComponents.Count > 0)
        {
            result.Status = "document_changed";
            result.Message = "Подпись валидна, но после подписания изменено: " + Changed();
        }
        else if (result.SignatureVerified is null)
        {
            result.Status = "provider_unavailable";
            result.Message = "Записка не менялась, но сервис проверки ЭЦП сейчас недоступен — подпись не перепроверена";
        }
        else
        {
            result.Message = "Подпись валидна, записка и резолюция не менялись после подписания";
        }

        return result;
    }

    /// <summary>Записка, если текущий пользователь её видит (то же правило, что у печатной формы).</summary>
    private async Task<SzDocument> LoadVisibleAsync(int szId, int currentUserId, CancellationToken ct)
    {
        var sz = await _db.SzDocuments.AsNoTracking()
                     .Include(x => x.Document)
                     .FirstOrDefaultAsync(x => x.Id == szId, ct)
                 ?? throw new KeyNotFoundException("Служебная записка не найдена");

        var canSeeAll = _currentUser.HasPermission(PermissionCode.RegisterSz)
                        || _currentUser.HasPermission(PermissionCode.ViewAllSz);
        if (!await SzVisibility.CanViewAsync(_db, szId, currentUserId, canSeeAll,
                _currentUser.HasPermission(PermissionCode.ManageSystemSettings)))
            throw new UnauthorizedAccessException("Нет доступа к этой записке");

        return sz;
    }

    // ----------------------------------------------------------------------------------------
    // ТЕСТ: имитация ЭЦП (только EcpSigning:AllowSzTestEcp)
    // ----------------------------------------------------------------------------------------

    public const string TestPin = "00000000000000";
    public const string TestLevelTitle = "ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП (не является подписью)";
    public const string TestVerificationNote = "ТЕСТ: имитация ЭЦП, у ГП «Инфоком» подпись не ставилась и не проверялась";

    public async Task ResolveWithTestEcpAsync(
        int szId, SzStoredEcpSignRequest request, int currentUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (sz, participant) = await LoadForDecisionAsync(
            szId, request.ParticipantId, request.Decision, request.Comment, currentUserId, ct);

        // Хэш — тот же, что подписывался бы по-настоящему: лист согласования и сверка версий текста
        // работают с тестовой подписью так же, как с боевой.
        var contentDigest = await BuildContentDigestAsync(sz, ct);
        var components = SzResolutionSignatureHasher.CreateComponents(
            request.Decision, request.Comment, contentDigest, sz.DocumentId);
        var hash = SzResolutionSignatureHasher.Compute(components);

        // «Подпись» — случайные байты: уникальна (индекс по SHA-256 подписи) и заведомо не CMS.
        var fakeSignature = RandomNumberGenerator.GetBytes(64);
        var now = DateTime.UtcNow;

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.FullName, Position = u.Position != null ? u.Position.TitleRu : null })
            .FirstOrDefaultAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var signature = new Signature
        {
            DocumentId = sz.DocumentId,
            ContentHash = contentDigest,
            UserId = currentUserId,
            Level = SignatureLevel.Qualified,
            At = now,
            StampMeta = JsonSerializer.Serialize(new
            {
                fullName = user?.FullName,
                position = user?.Position,
                levelTitle = TestLevelTitle,
                ecp = true,
                test = true,
            }),
            Revoked = false,
        };
        _db.Signatures.Add(signature);
        await _db.SaveChangesAsync(ct);

        _db.SzResolutionSignatures.Add(new SzResolutionSignature
        {
            SzDocumentId = sz.Id,
            DocumentId = sz.DocumentId,
            RouteParticipantId = participant.Id,
            SignatureId = signature.Id,
            Decision = request.Decision,
            UserId = currentUserId,

            PersonIdnp = TestPin,
            PersonFio = Truncate(user?.FullName, 300),
            CertCommonName = Truncate(user?.FullName, 300),
            CertValidNotAfter = now.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CertIssuerCommonName = "Тестовый УЦ (имитация)",

            HashSchemaVersion = SzResolutionSignatureHasher.SchemaVersion,
            Hash = hash,
            CommentText = components.Comment,
            CommentHash = ResolutionSignatureHasher.Sha256Hex(components.Comment),
            ContentDigest = contentDigest,
            SignatureBase64 = Convert.ToBase64String(fakeSignature),
            SignatureSha256 = ResolutionSignatureHasher.Sha256Hex(fakeSignature),

            SignedAt = now,
            VerifiedAt = now,
            // Verified=false — признак тестовой имитации: такие записи не участвуют в привязке ПИН
            // и печатаются в листе как «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП».
            Verified = false,
            VerificationError = TestVerificationNote,

            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
        });
        await _db.SaveChangesAsync(ct);

        await _routeEngine.ResolveAsync(participant.Id, request.Decision,
            string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            currentUserId, signature.Id);

        await tx.CommitAsync(ct);

        _logger.LogWarning("СЗ {SzId}: решение {Decision} пользователя {UserId} сохранено с ТЕСТОВОЙ имитацией ЭЦП",
            szId, request.Decision, currentUserId);
    }

    // ----------------------------------------------------------------------------------------
    // Внутреннее
    // ----------------------------------------------------------------------------------------

    /// <summary>Записка + активный участник маршрута этой записки, решение которого выносит текущий
    /// пользователь (сам или как замещающий). Проверяет и то, что решение подписываемое, а для
    /// замечаний/отклонения есть комментарий — до обращений к провайдеру.</summary>
    private async Task<(SzDocument Sz, RouteParticipant Participant)> LoadForDecisionAsync(
        int szId, int participantId, ResolutionType decision, string? comment, int currentUserId,
        CancellationToken ct)
    {
        if (!SzResolutionSignatureHasher.IsSignable(decision))
            throw new InvalidOperationException("Для этого решения подпись ЭЦП не предусмотрена");
        if (decision is ResolutionType.ApprovedWithRemarks or ResolutionType.Rejected
            && string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("Комментарий обязателен для замечаний и отклонения");
        if (comment is { Length: > MaxCommentLength })
            throw new InvalidOperationException($"Комментарий не может превышать {MaxCommentLength} символов");

        var sz = await _db.SzDocuments.AsNoTracking()
                     .Include(x => x.Document)
                     .FirstOrDefaultAsync(x => x.Id == szId, ct)
                 ?? throw new KeyNotFoundException("Служебная записка не найдена");

        var participant = await _db.RouteParticipants.AsNoTracking()
                              .Include(p => p.RouteStep).ThenInclude(s => s!.RouteInstance)
                              .FirstOrDefaultAsync(p => p.Id == participantId, ct)
                          ?? throw new KeyNotFoundException("Этап согласования не найден");

        if (participant.RouteStep?.RouteInstance?.DocumentId != sz.DocumentId)
            throw new KeyNotFoundException("Этап согласования не относится к этой записке");
        if (participant.State != ParticipantState.Active)
            throw new InvalidOperationException("Решение по этому этапу уже вынесено или ещё не время");

        if (participant.UserId is { } assignee && assignee != currentUserId)
        {
            var actingFor = await _substitutions.GetActingForUserIdsAsync(currentUserId);
            if (!actingFor.Contains(assignee))
                throw new UnauthorizedAccessException(
                    "Решение по этому этапу выносит назначенный согласующий или его замещающий");
        }

        return (sz, participant);
    }

    private Task<string> BuildContentDigestAsync(SzDocument sz, CancellationToken ct) =>
        SzEcpSheetData.ContentDigestAsync(_db, sz, ct);

    /// <summary>Привязка ПИН ↔ учётка delosfera по журналам подписей ВНД и СЗ: первая принятая подпись
    /// закрепляет ПИН за учёткой; ЭЦП, подписывавшая за одного пользователя, не принимается от другого.</summary>
    private async Task EnsurePinBindingAsync(int userId, string signerPin, CancellationToken ct)
    {
        var boundToOtherPin =
            await _db.VndResolutionSignatures.AnyAsync(s => s.Verified && s.UserId == userId && s.PersonIdnp != signerPin, ct)
            // Тестовые имитации (Verified=false) к привязке ПИН отношения не имеют.
            || await _db.SzResolutionSignatures.AnyAsync(s => s.Verified && s.UserId == userId && s.PersonIdnp != signerPin, ct);
        if (boundToOtherPin)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Ваша учётная запись уже привязана к ЭЦП другого лица — подписывать можно только своей ЭЦП");

        var usedByOtherUser =
            await _db.VndResolutionSignatures.AnyAsync(s => s.Verified && s.PersonIdnp == signerPin && s.UserId != userId, ct)
            || await _db.SzResolutionSignatures.AnyAsync(s => s.Verified && s.PersonIdnp == signerPin && s.UserId != userId, ct);
        if (usedByOtherUser)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Эта ЭЦП уже использовалась другим пользователем системы — подпись отклонена");
    }

    /// <summary>ГП на неверную подпись отвечает HTTP 500 с текстом "Ошибка при проверке подписи…" —
    /// это отказ по подписи, а не недоступность (см. ResolutionSignatureService).</summary>
    private static bool IsInvalidSignatureError(InfocomEcpException ex) =>
        ex.StatusCode == 500
        && ex.Error.ErrorMessage?.Contains("Ошибка при проверке подписи", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>2.6. null — провайдер недоступен; отказ по подписи — Verified=false.</summary>
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
            _logger.LogWarning("СЗ 2.6 check-sign: провайдер отклонил подпись (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return new InfocomCheckSignResult { Verified = false };
        }
        catch (InfocomEcpException ex)
        {
            _logger.LogWarning("СЗ 2.6 check-sign: ошибка ecp-service/провайдера (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "СЗ 2.6 check-sign: нет связи с delosfera-ecp-service");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("СЗ 2.6 check-sign: таймаут delosfera-ecp-service");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "СЗ 2.6 check-sign: неожиданный формат ответа");
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
            if (!string.Equals(FormatPin(cert.PersonIdnp), signerPin, StringComparison.Ordinal))
            {
                _logger.LogWarning("СЗ 2.4 get-cert-info: ПИН сертификата не совпал с владельцем подписи — сведения не сохранены");
                return null;
            }
            return cert;
        }
        catch (Exception ex) when (ex is InfocomEcpException or HttpRequestException or JsonException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.LogInformation("СЗ 2.4 get-cert-info недоступен ({Error}) — сведения о сертификате не сохранены",
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
            {
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
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? FormatPin(long? pin) =>
        pin is > 0 ? pin.Value.ToString("D14", CultureInfo.InvariantCulture) : null;

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}

/// <summary>
/// Данные ЭЦП для листа согласования служебной записки: отпечаток текущего содержимого и штампы по
/// подписям контура. Статические — чтобы печатная форма (SzPaperService) брала их своим контекстом БД,
/// не обрастая новыми зависимостями.
/// </summary>
public static class SzEcpSheetData
{
    /// <summary>Отпечаток содержимого записки на текущий момент (см. SzResolutionSignatureHasher.BuildContentDigest).</summary>
    public static async Task<string> ContentDigestAsync(DelosferaDbContext db, SzDocument sz, CancellationToken ct = default)
    {
        var document = sz.Document ?? await db.Documents.AsNoTracking().FirstAsync(d => d.Id == sz.DocumentId, ct);

        var attachments = await db.DocumentAttachments.AsNoTracking()
            .Where(a => a.DocumentId == sz.DocumentId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Hash })
            .ToListAsync(ct);

        return SzResolutionSignatureHasher.BuildContentDigest(new SzContentMaterial(
            sz.DocumentId,
            document.RegNumber,
            document.Title,
            sz.KindId,
            sz.HrKindId,
            sz.AddresseeUserId,
            sz.CorrespondentUnitId,
            sz.Body,
            sz.EmployeeName,
            sz.EmployeeUnitId,
            sz.TransferUnitId,
            sz.Amount,
            sz.TravelExpenses,
            sz.ExtraFields?.RootElement.GetRawText(),
            attachments.Select(a => (a.Id, (string?)a.Hash)).ToList()));
    }

    /// <summary>Сведения ЭЦП по подписям контура: Signature.Id → штамп. Подписи без записи в журнале
    /// ЭЦП (простые, квалифицированные ТУМАР/ОЭП) в словарь не попадают.</summary>
    public static async Task<Dictionary<int, SzEcpStampInfo>> StampsAsync(
        DelosferaDbContext db, IReadOnlyCollection<int> signatureIds, CancellationToken ct = default)
    {
        if (signatureIds.Count == 0) return new Dictionary<int, SzEcpStampInfo>();

        var rows = await db.SzResolutionSignatures.AsNoTracking()
            .Where(s => signatureIds.Contains(s.SignatureId))
            .Select(s => new
            {
                s.SignatureId,
                s.PersonFio,
                s.CertCommonName,
                s.OrganizationName,
                s.CertValidNotAfter,
                s.CertIssuerCommonName,
                s.SignedAt,
                s.Hash,
                s.SignatureSha256,
                s.ContentDigest,
                s.Verified,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.SignatureId)
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.First();
                return new SzEcpStampInfo(r.PersonFio ?? r.CertCommonName, r.OrganizationName, r.CertValidNotAfter,
                    r.CertIssuerCommonName, r.SignedAt, r.Hash, r.SignatureSha256, r.ContentDigest, !r.Verified);
            });
    }
}
