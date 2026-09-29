using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Signing.Models.InfocomEcp;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Машиночитаемые коды ошибок проверки подписи — по ним клиент решает, что показать
/// (например, на <see cref="DocumentChanged"/> предлагает подписать заново).</summary>
public static class ResolutionSignatureErrorCodes
{
    public const string EcpRequired = "ecp_required";
    public const string InvalidPin = "invalid_pin";
    public const string DocumentChanged = "document_changed";
    public const string SignatureInvalid = "signature_invalid";
    public const string SignatureReused = "signature_reused";
    public const string StaleSignature = "stale_signature";
    public const string IdentityMismatch = "identity_mismatch";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string StorageIntegrity = "storage_integrity";
}

/// <summary>Отказ в приёме подписи/хэша — контроллер отдаёт StatusCode + { message, code }.</summary>
public class ResolutionSignatureException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

/// <summary>Всё, что нужно для проверки подписи резолюции "Согласовать" в момент сохранения.</summary>
public sealed class ResolutionSignatureInput
{
    /// <summary>Процесс, загруженный с Redaction и Vnd (как в VndApprovalService.LoadProcessForVndAsync).</summary>
    public required VndApprovalProcess Process { get; init; }
    public required VndApprovalStage Stage { get; init; }
    public required ApprovalStageDecision Decision { get; init; }
    public required int UserId { get; init; }

    public string? Comment { get; init; }
    public string? SignatureBase64 { get; init; }
    public string? Hash { get; init; }

    /// <summary>ПИН, введённый пользователем при получении токена (2.3) — сверяется с ПИН из 2.6.</summary>
    public string? PersonIdnp { get; init; }

    /// <summary>Токен Инфокома — только для best-effort 2.4 прямо сейчас, НЕ сохраняется.</summary>
    public string? CertToken { get; init; }

    /// <summary>timestamp из ответа 2.5 (Unix ms), как его прислал клиент.</summary>
    public long? ProviderTimestampMs { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public interface IResolutionSignatureService
{
    /// <summary>Хэш резолюции для подписи: комментарий — из запроса, содержимое редакции и код ВНД —
    /// из БД. Только для назначенного согласующего этого этапа на активной фазе.</summary>
    Task<ResolutionHashResponse> GetHashForStageAsync(
        int vndId, int stageId, string? comment, ApprovalStageDecision decision, int currentUserId,
        CancellationToken ct = default);

    /// <summary>Полная проверка подписи перед сохранением резолюции "Согласовать" (хэш из БД,
    /// replay, свежесть, 2.6, личность подписанта). Бросает <see cref="ResolutionSignatureException"/>
    /// при любом несоответствии (fail closed). Возвращает ещё НЕ добавленную в контекст запись
    /// журнала — вызывающий код сохраняет её в одном SaveChanges с решением.</summary>
    Task<VndResolutionSignature> VerifyForDecisionAsync(ResolutionSignatureInput input, CancellationToken ct = default);

    Task<List<VndResolutionSignatureResponse>> ListAsync(int vndId, int currentUserId, CancellationToken ct = default);

    Task<ResolutionSignatureVerifyResponse> ReverifyAsync(
        int vndId, int signatureId, int currentUserId, CancellationToken ct = default);
}

/// <summary>
/// ЭЦП ГП «Инфоком» на резолюции "Согласовать" (SIG-ECP): хэш резолюции, проверка подписи при
/// сохранении и журнал подписей (<see cref="VndResolutionSignature"/>).
///
/// Порядок проверки при сохранении (всё до записи решения; любая ошибка — отказ, решение не
/// сохраняется):
///  1. подпись/хэш/ПИН присутствуют, ПИН — 14 цифр;
///  2. хэш пересчитывается из БД (комментарий из запроса + текущая редакция + код ВНД) и
///     сравнивается с присланным за постоянное время;
///  3. эту же подпись ещё не предъявляли (уникальный SHA-256 подписи);
///  4. подпись свежая (signingTime из CMS, иначе timestamp 2.5) — не старше 5 минут;
///  5. 2.6 check-sign вызывается с ПЕРЕСЧИТАННЫМ хэшем; verified != true — отказ;
///     ГП недоступен/таймаут — отказ (fail closed);
///  6. ПИН владельца подписи из 2.6 совпадает с введённым ПИН и с ПИН, к которому учётка уже
///     привязана журналом (привязка "при первом подписании": первая успешная подпись закрепляет
///     ПИН за учёткой, дальше требуется совпадение; и наоборот — одна ЭЦП не может подписывать
///     за двух разных пользователей delosfera);
///  7. best-effort 2.4 (сведения о сертификате) — только если ПИН в сертификате тот же.
/// </summary>
public class ResolutionSignatureService : IResolutionSignatureService
{
    /// <summary>Подпись старше этого — отклоняется (защита от повторного использования старой подписи).</summary>
    public static readonly TimeSpan MaxSignatureAge = TimeSpan.FromMinutes(5);

    /// <summary>Допуск на расхождение часов с провайдером для времени подписи "из будущего".</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);

    // Совпадает с MaxResolutionCommentLength в VndApprovalService.
    private const int MaxCommentLength = 35000;

    // CMS-подпись хэша — единицы килобайт (сертификат + подпись); 256 КБ — с большим запасом,
    // защита от мусора размером с документ в поле формы.
    private const int MaxSignatureBytes = 256 * 1024;

    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    private static readonly JsonSerializerOptions ProviderJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IInfocomEcpClient _infocom;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ResolutionSignatureService> _logger;

    public ResolutionSignatureService(
        DelosferaDbContext db,
        IFileStorageService files,
        IInfocomEcpClient infocom,
        ICurrentUserService currentUser,
        ILogger<ResolutionSignatureService> logger)
    {
        _db = db;
        _files = files;
        _infocom = infocom;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ----------------------------------------------------------------------------------------
    // Хэш для подписи
    // ----------------------------------------------------------------------------------------

    public async Task<ResolutionHashResponse> GetHashForStageAsync(
        int vndId, int stageId, string? comment, ApprovalStageDecision decision, int currentUserId,
        CancellationToken ct = default)
    {
        EnsureSignableDecision(decision);
        if (comment is { Length: > MaxCommentLength })
            throw new InvalidOperationException($"Комментарий не может превышать {MaxCommentLength} символов");

        var process = await LoadActiveProcessAsync(vndId, ct);

        var stage = process.Stages.FirstOrDefault(s => s.Id == stageId)
                    ?? throw new KeyNotFoundException($"Этап согласования с id={stageId} не найден");

        // Хэш отдаём только тому, кто реально может подписать эту резолюцию: он зависит от
        // содержимого документа, посторонним он ни к чему.
        if (stage.ApproverUserId != currentUserId)
            throw new UnauthorizedAccessException("Вы не назначены согласующим на этом этапе");
        if (stage.IsRemovedByEditor)
            throw new InvalidOperationException(
                "Вы исключены из маршрута согласования главным редактором — решение принять нельзя");

        _ = PhaseForStatus(process.Status);

        var components = await BuildComponentsAsync(process.Redaction!, process.Vnd!.Code, comment, decision, ct);
        return new ResolutionHashResponse
        {
            Hash = ResolutionSignatureHasher.Compute(components),
            SchemaVersion = ResolutionSignatureHasher.VersionOf(components),
        };
    }

    // ----------------------------------------------------------------------------------------
    // Проверка при сохранении резолюции
    // ----------------------------------------------------------------------------------------

    public async Task<VndResolutionSignature> VerifyForDecisionAsync(
        ResolutionSignatureInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var process = input.Process;
        var redaction = process.Redaction
                        ?? throw new InvalidOperationException("Процесс согласования загружен без редакции");
        var vnd = process.Vnd
                  ?? throw new InvalidOperationException("Процесс согласования загружен без ВНД");

        // 1. Наличие и формат.
        if (string.IsNullOrWhiteSpace(input.SignatureBase64)
            || string.IsNullOrWhiteSpace(input.Hash)
            || string.IsNullOrWhiteSpace(input.PersonIdnp))
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.EcpRequired,
                "Для резолюции «Согласовать» требуется подпись ЭЦП");

        var claimedPin = input.PersonIdnp.Trim();
        if (!InfocomFieldRules.IsValidPin(claimedPin))
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.InvalidPin, InfocomFieldRules.PinError);

        var signatureBase64 = input.SignatureBase64.Trim();
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

        var phase = PhaseForStatus(process.Status);
        var roundNumber = await CurrentRoundNumberAsync(process.Id, phase, ct);

        // 2. Хэш — только из актуальных данных БД.
        EnsureSignableDecision(input.Decision);
        var components = await BuildComponentsAsync(redaction, vnd.Code, input.Comment, input.Decision, ct);
        var expectedHash = ResolutionSignatureHasher.Compute(components);
        if (!ResolutionSignatureHasher.HashesEqual(expectedHash, input.Hash))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.DocumentChanged,
                "Документ или комментарий изменились после подписания — подпишите заново");

        // 3. Replay: та же подпись второй раз не принимается.
        var signatureSha256 = ResolutionSignatureHasher.Sha256Hex(signatureBytes);
        if (await _db.VndResolutionSignatures.AnyAsync(s => s.SignatureSha256 == signatureSha256, ct))
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.SignatureReused,
                "Эта подпись уже была использована — подпишите резолюцию заново");

        // 4. Свежесть. signingTime внутри CMS подписан провайдером — ему доверяем; timestamp 2.5
        // приходит от клиента и сам по себе подделываем, поэтому используется только если CMS
        // времени не содержит (и тогда подстраховкой остаются п.3 и привязка к хэшу документа).
        var now = DateTime.UtcNow;
        var cmsSigningTime = TryReadCmsSigningTime(signatureBytes);
        var providerTimestamp = FromUnixMs(input.ProviderTimestampMs);
        var signingTime = cmsSigningTime ?? providerTimestamp;
        if (signingTime is null)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.StaleSignature,
                "Не удалось определить время подписи — подпишите резолюцию заново");
        if (signingTime.Value < now - MaxSignatureAge)
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.StaleSignature,
                $"Подпись старше {MaxSignatureAge.TotalMinutes:0} минут — подпишите резолюцию заново");
        if (signingTime.Value > now + MaxClockSkew)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Время подписи указано в будущем — подпись отклонена");

        // 5. 2.6 — с ПЕРЕСЧИТАННЫМ хэшем, не с присланным.
        var check = await CheckSignAsync(signatureBase64, expectedHash, ct)
                    ?? throw new ResolutionSignatureException(StatusCodes.Status503ServiceUnavailable,
                        ResolutionSignatureErrorCodes.ProviderUnavailable,
                        "Сервис проверки ЭЦП недоступен — резолюция не сохранена, попробуйте позже");
        if (!check.Verified)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Подпись ЭЦП не прошла проверку у провайдера — резолюция не сохранена");

        // 6. Личность: ПИН из 2.6 (не из ввода пользователя) — источник правды.
        var signerPin = FormatPin(check.PersonIdnp);
        if (signerPin is null)
            throw new ResolutionSignatureException(StatusCodes.Status400BadRequest,
                ResolutionSignatureErrorCodes.SignatureInvalid,
                "Провайдер не вернул ПИН владельца подписи — резолюция не сохранена");
        if (!string.Equals(signerPin, claimedPin, StringComparison.Ordinal))
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Подпись принадлежит другому лицу, а не владельцу введённого ПИН");

        await EnsurePinBindingAsync(input.UserId, signerPin, ct);

        // 7. Сертификат — не обязателен, и только если это сертификат того же лица.
        var cert = await TryGetCertInfoAsync(input.CertToken, signerPin, ct);

        return new VndResolutionSignature
        {
            VndId = vnd.Id,
            ApprovalProcessId = process.Id,
            RedactionId = redaction.Id,
            StageId = input.Stage.Id,
            Phase = phase,
            RoundNumber = roundNumber,
            Decision = input.Decision,
            UserId = input.UserId,

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

            HashSchemaVersion = ResolutionSignatureHasher.VersionOf(components),
            Hash = expectedHash,
            CommentText = components.Comment,
            CommentHash = ResolutionSignatureHasher.Sha256Hex(components.Comment),
            ContentDigest = components.Content,
            VndCode = Truncate(components.Code, 100)!,
            SignatureBase64 = signatureBase64,
            SignatureSha256 = signatureSha256,

            SignedAt = now,
            ProviderTimestamp = providerTimestamp,
            CmsSigningTime = cmsSigningTime,
            VerifiedAt = DateTime.UtcNow,
            ProviderCheckTimestamp = FromUnixMs(check.Timestamp),
            Verified = true,
            VerificationError = null,

            IpAddress = Truncate(input.IpAddress, 64),
            UserAgent = Truncate(input.UserAgent, 512),
        };
    }

    // ----------------------------------------------------------------------------------------
    // Журнал и повторная проверка
    // ----------------------------------------------------------------------------------------

    public async Task<List<VndResolutionSignatureResponse>> ListAsync(
        int vndId, int currentUserId, CancellationToken ct = default)
    {
        await EnsureCanViewAsync(vndId, currentUserId, ct);

        return await _db.VndResolutionSignatures
            .AsNoTracking()
            .Where(s => s.VndId == vndId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new VndResolutionSignatureResponse
            {
                Id = s.Id,
                VndId = s.VndId,
                ApprovalProcessId = s.ApprovalProcessId,
                RedactionId = s.RedactionId,
                StageId = s.StageId,
                StageTitle = s.Stage!.Title,
                Phase = s.Phase,
                RoundNumber = s.RoundNumber,
                UserId = s.UserId,
                UserFullName = s.User!.FullName,
                PersonIdnp = s.PersonIdnp,
                PersonFio = s.PersonFio,
                OrganizationName = s.OrganizationName,
                CertCommonName = s.CertCommonName,
                CertKeyIdentifier = s.CertKeyIdentifier,
                CertValidNotAfter = s.CertValidNotAfter,
                CertIssuerCommonName = s.CertIssuerCommonName,
                HashSchemaVersion = s.HashSchemaVersion,
                Hash = s.Hash,
                SignatureSha256 = s.SignatureSha256,
                SignedAt = s.SignedAt,
                VerifiedAt = s.VerifiedAt,
                Verified = s.Verified,
            })
            .ToListAsync(ct);
    }

    public async Task<ResolutionSignatureVerifyResponse> ReverifyAsync(
        int vndId, int signatureId, int currentUserId, CancellationToken ct = default)
    {
        await EnsureCanViewAsync(vndId, currentUserId, ct);

        var entry = await _db.VndResolutionSignatures
                         .AsNoTracking()
                         .FirstOrDefaultAsync(s => s.Id == signatureId && s.VndId == vndId, ct)
                     ?? throw new KeyNotFoundException($"Подпись с id={signatureId} не найдена");

        var result = new ResolutionSignatureVerifyResponse
        {
            Status = "valid",
            Message = "",
            CheckedAt = DateTime.UtcNow,
        };

        if (entry.HashSchemaVersion is not (ResolutionSignatureHasher.SchemaVersion or ResolutionSignatureHasher.SchemaVersionV2))
        {
            result.Status = "unsupported_schema";
            result.Message = $"Неизвестная версия схемы хэша \"{entry.HashSchemaVersion}\"";
            return result;
        }

        // Запись журнала сама с собой: хранимые компоненты должны давать хранимый хэш. Если нет —
        // строку правили в обход приложения (триггер это запрещает, но проверка дешёвая).
        var storedComponents = new ResolutionHashComponents(entry.CommentText, entry.ContentDigest, entry.VndCode,
            entry.HashSchemaVersion == ResolutionSignatureHasher.SchemaVersionV2
                ? ResolutionSignatureHasher.DecisionCode(entry.Decision)
                : null);
        var journalConsistent =
            ResolutionSignatureHasher.HashesEqual(entry.Hash, ResolutionSignatureHasher.Compute(storedComponents))
            && ResolutionSignatureHasher.HashesEqual(entry.CommentHash,
                ResolutionSignatureHasher.Sha256Hex(entry.CommentText));

        // Текущее состояние: резолюция на этапе, файлы редакции, код ВНД.
        var liveComment = await FindLiveDecisionCommentAsync(entry, ct);
        if (liveComment.Found
            && !ResolutionSignatureHasher.HashesEqual(entry.CommentHash,
                ResolutionSignatureHasher.Sha256Hex(ResolutionSignatureHasher.NormalizeComment(liveComment.Comment))))
            result.ChangedComponents.Add("comment");

        var redaction = await _db.VndRedactions.AsNoTracking().FirstAsync(r => r.Id == entry.RedactionId, ct);
        string? liveContent;
        try
        {
            liveContent = await BuildContentDigestAsync(redaction, ct);
        }
        catch (ResolutionSignatureException ex) when (ex.Code == ResolutionSignatureErrorCodes.StorageIntegrity)
        {
            liveContent = null;
        }
        if (liveContent is null || !string.Equals(liveContent, entry.ContentDigest, StringComparison.Ordinal))
            result.ChangedComponents.Add("content");

        var liveCode = await _db.VndDocuments.AsNoTracking()
            .Where(v => v.Id == vndId).Select(v => v.Code).FirstAsync(ct);
        if (!string.Equals(ResolutionSignatureHasher.NormalizeCode(liveCode), entry.VndCode, StringComparison.Ordinal))
            result.ChangedComponents.Add("code");

        // Сама подпись — у провайдера, по сохранённому хэшу (он и подписывался).
        var check = await CheckSignAsync(entry.SignatureBase64, entry.Hash, ct);
        result.SignatureVerified = check is null
            ? null
            : check.Verified && string.Equals(FormatPin(check.PersonIdnp), entry.PersonIdnp, StringComparison.Ordinal);
        result.SignerPersonFio = check?.PersonFio ?? entry.PersonFio;

        if (!journalConsistent)
        {
            result.Status = "journal_tampered";
            result.Message = "Запись журнала подписей не согласована сама с собой — данные изменены в обход системы";
        }
        else if (result.SignatureVerified == false)
        {
            result.Status = "signature_invalid";
            result.Message = "Провайдер не подтвердил подпись или её владельца";
        }
        else if (result.ChangedComponents.Count > 0)
        {
            result.Status = "document_changed";
            result.Message = "Документ изменён после подписания: " + string.Join(", ",
                result.ChangedComponents.Select(c => c switch
                {
                    "comment" => "текст резолюции",
                    "content" => "содержимое редакции (файлы документа)",
                    "code" => "код ВНД",
                    _ => c,
                }));
        }
        else if (result.SignatureVerified is null)
        {
            result.Status = "provider_unavailable";
            result.Message = "Документ не менялся, но сервис проверки ЭЦП сейчас недоступен — подпись не перепроверена";
        }
        else
        {
            result.Message = "Подпись валидна, документ и резолюция не менялись после подписания";
        }

        return result;
    }

    // ----------------------------------------------------------------------------------------
    // Внутреннее
    // ----------------------------------------------------------------------------------------

    private async Task<VndApprovalProcess> LoadActiveProcessAsync(int vndId, CancellationToken ct)
    {
        // Та же логика, что VndApprovalService.LoadProcessForVndAsync: последняя редакция →
        // её последний процесс согласования.
        var lastRedactionId = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .OrderByDescending(r => r.Number)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"У ВНД с id={vndId} нет редакций");

        return await _db.VndApprovalProcesses
                   .AsNoTracking()
                   .Include(x => x.Stages)
                   .Include(x => x.Redaction)
                   .Include(x => x.Vnd)
                   .Where(x => x.RedactionId == lastRedactionId)
                   .OrderByDescending(x => x.CreatedAt)
                   .FirstOrDefaultAsync(ct)
               ?? throw new KeyNotFoundException("Для последней редакции согласование не запускалось");
    }

    private static ApprovalStagePhase PhaseForStatus(ApprovalProcessStatus status) => status switch
    {
        ApprovalProcessStatus.Primary => ApprovalStagePhase.Primary,
        ApprovalProcessStatus.Repeated => ApprovalStagePhase.Repeat,
        ApprovalProcessStatus.FinalHold => ApprovalStagePhase.FinalHold,
        _ => throw new InvalidOperationException("В текущем статусе процесса принятие решений недоступно"),
    };

    private async Task<int> CurrentRoundNumberAsync(int processId, ApprovalStagePhase phase, CancellationToken ct)
    {
        if (phase == ApprovalStagePhase.Primary) return 1;
        return await _db.VndApprovalPhaseRounds
            .CountAsync(r => r.ApprovalProcessId == processId && r.Phase == phase, ct) + 1;
    }

    private async Task<ResolutionHashComponents> BuildComponentsAsync(
        VndRedaction redaction, string vndCode, string? comment, ApprovalStageDecision decision, CancellationToken ct)
    {
        var content = await BuildContentDigestAsync(redaction, ct);
        return ResolutionSignatureHasher.CreateComponents(comment, content, vndCode, decision);
    }

    /// <summary>Подписываются только решения согласующего: "Согласовать", "с замечаниями", "Отклонить".</summary>
    private static void EnsureSignableDecision(ApprovalStageDecision decision)
    {
        if (decision is not (ApprovalStageDecision.Approved or ApprovalStageDecision.ApprovedWithComment
            or ApprovalStageDecision.Rejected))
            throw new InvalidOperationException("Для этого решения подпись ЭЦП не предусмотрена");
    }

    private async Task<string> BuildContentDigestAsync(VndRedaction redaction, CancellationToken ct)
    {
        var ru = await FileSha256Async(redaction.DocFileRuId, ct);
        var kg = redaction.DocFileKgId is int kgId ? await FileSha256Async(kgId, ct) : null;
        var en = redaction.DocFileEnId is int enId ? await FileSha256Async(enId, ct) : null;
        return ResolutionSignatureHasher.BuildContentDigest(ru, kg, en);
    }

    /// <summary>SHA-256 реальных байтов файла из хранилища (а не сохранённого при загрузке
    /// значения) — так подпись подтверждает то, что лежит в MinIO прямо сейчас. Если при загрузке
    /// был зафиксирован хэш (FileAttachment.Hash) и он не совпал — объект в хранилище подменён,
    /// подписание останавливается.</summary>
    private async Task<string> FileSha256Async(int fileId, CancellationToken ct)
    {
        var storedHash = await _db.FileAttachments.AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => f.Hash)
            .FirstOrDefaultAsync(ct);

        var (stream, _, _) = await _files.DownloadAsync(fileId, ct);
        string actual;
        await using (stream)
        {
            actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        }

        if (!string.IsNullOrEmpty(storedHash) && !ResolutionSignatureHasher.HashesEqual(storedHash, actual))
        {
            _logger.LogError(
                "Файл {FileId} в хранилище не совпадает с SHA-256, зафиксированным при загрузке", fileId);
            throw new ResolutionSignatureException(StatusCodes.Status409Conflict,
                ResolutionSignatureErrorCodes.StorageIntegrity,
                "Файл документа в хранилище не совпадает с контрольной суммой, зафиксированной при " +
                "загрузке — подписание остановлено, обратитесь к администратору");
        }

        return actual;
    }

    /// <summary>2.6. null — провайдер/ecp-service недоступен (сеть, таймаут, 5xx); ответ провайдера
    /// "подпись некорректна" (4xx) превращается в Verified=false.</summary>
    /// <summary>ГП на неверную/испорченную подпись отвечает НЕ 4xx, а errorCode/HTTP 500 с текстом
    /// "Ошибка при проверке подписи: Подпись не верна" (или "...: java.lang.NullPointerException" для
    /// битой CMS) — см. документацию 2.6. Это отказ по подписи, а не недоступность провайдера, иначе
    /// пользователь увидит "сервис недоступен" вместо предложения подписать заново. Любые другие 5xx
    /// по-прежнему считаются недоступностью (fail closed, решение не сохраняется).</summary>
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
            _logger.LogWarning("2.6 check-sign: провайдер отклонил подпись (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return new InfocomCheckSignResult { Verified = false };
        }
        catch (InfocomEcpException ex)
        {
            _logger.LogWarning("2.6 check-sign: ошибка ecp-service/провайдера (HTTP {Status}, errorCode {ErrorCode})",
                ex.StatusCode, ex.Error.ErrorCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "2.6 check-sign: нет связи с delosfera-ecp-service");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("2.6 check-sign: таймаут delosfera-ecp-service");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "2.6 check-sign: неожиданный формат ответа");
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

            // Токен прислал клиент — сведения о сертификате берём, только если это сертификат того
            // же лица, что подтвердил провайдер в 2.6; иначе молча не сохраняем их.
            if (!string.Equals(FormatPin(cert.PersonIdnp), signerPin, StringComparison.Ordinal))
            {
                _logger.LogWarning("2.4 get-cert-info: ПИН сертификата не совпал с владельцем подписи — сведения не сохранены");
                return null;
            }
            return cert;
        }
        catch (Exception ex) when (ex is InfocomEcpException or HttpRequestException or JsonException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Некритично: подпись уже проверена в 2.6, без сведений о сертификате запись журнала полна.
            _logger.LogInformation("2.4 get-cert-info недоступен ({Error}) — сведения о сертификате не сохранены",
                ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Привязка ПИН ↔ учётка delosfera по журналу подписей ("при первом подписании"):
    /// в профиле пользователя поля ПИН нет, поэтому источником привязки служит сам неизменяемый
    /// журнал. Первая принятая подпись закрепляет ПИН; дальше — только он. И наоборот: ЭЦП, уже
    /// подписывавшая за одного пользователя delosfera, не принимается от другого.</summary>
    private async Task EnsurePinBindingAsync(int userId, string signerPin, CancellationToken ct)
    {
        var boundToOtherPin = await _db.VndResolutionSignatures
            .AnyAsync(s => s.UserId == userId && s.PersonIdnp != signerPin, ct);
        if (boundToOtherPin)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Ваша учётная запись уже привязана к ЭЦП другого лица — подписывать можно только своей ЭЦП");

        var usedByOtherUser = await _db.VndResolutionSignatures
            .AnyAsync(s => s.PersonIdnp == signerPin && s.UserId != userId, ct);
        if (usedByOtherUser)
            throw new ResolutionSignatureException(StatusCodes.Status403Forbidden,
                ResolutionSignatureErrorCodes.IdentityMismatch,
                "Эта ЭЦП уже использовалась другим пользователем системы — подпись отклонена");
    }

    /// <summary>Видимость журнала — как у самого документа: ViewVnd проверяет контроллер; чужой
    /// черновик — только создателю, участникам его согласования или с правом ViewOtherUsersDrafts
    /// (то же правило, что VndFileAccessAuthorizer).</summary>
    private async Task EnsureCanViewAsync(int vndId, int currentUserId, CancellationToken ct)
    {
        var vnd = await _db.VndDocuments.AsNoTracking()
                      .Where(v => v.Id == vndId)
                      .Select(v => new { v.Status, v.CreatedByUserId })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Draft
            || vnd.CreatedByUserId == currentUserId
            || _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts))
            return;

        var isParticipant = await _db.VndApprovalStages
            .AnyAsync(s => s.ApprovalProcess!.VndId == vndId && s.ApproverUserId == currentUserId, ct);
        if (!isParticipant)
            throw new UnauthorizedAccessException("Нет доступа к документу");
    }

    private readonly record struct LiveComment(bool Found, string? Comment);

    /// <summary>Текущий текст резолюции, к которой относится подпись: Primary* на этапе; для
    /// повторных фаз — текущий круг на этапе или снимок завершённого круга.</summary>
    private async Task<LiveComment> FindLiveDecisionCommentAsync(VndResolutionSignature entry, CancellationToken ct)
    {
        var stage = await _db.VndApprovalStages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entry.StageId, ct);
        if (stage is null) return new LiveComment(false, null);

        if (entry.Phase == ApprovalStagePhase.Primary)
            return stage.PrimaryDecision == ApprovalStageDecision.Pending
                ? new LiveComment(false, null)
                : new LiveComment(true, stage.PrimaryComment);

        var archivedRounds = await _db.VndApprovalPhaseRounds
            .CountAsync(r => r.ApprovalProcessId == entry.ApprovalProcessId && r.Phase == entry.Phase, ct);

        if (entry.RoundNumber == archivedRounds + 1)
        {
            var (decision, comment) = entry.Phase == ApprovalStagePhase.Repeat
                ? (stage.RepeatDecision, stage.RepeatComment)
                : (stage.FinalHoldDecision, stage.FinalHoldComment);
            return decision is null or ApprovalStageDecision.Pending
                ? new LiveComment(false, null)
                : new LiveComment(true, comment);
        }

        var snapshot = await _db.VndApprovalPhaseRoundStageDecisions.AsNoTracking()
            .Where(d => d.VndApprovalStageId == entry.StageId
                        && d.VndApprovalPhaseRound!.ApprovalProcessId == entry.ApprovalProcessId
                        && d.VndApprovalPhaseRound.Phase == entry.Phase
                        && d.VndApprovalPhaseRound.RoundNumber == entry.RoundNumber)
            .Select(d => new { d.Comment })
            .FirstOrDefaultAsync(ct);
        return snapshot is null ? new LiveComment(false, null) : new LiveComment(true, snapshot.Comment);
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
            // Не CMS или непривычная структура — это не повод отклонять: подлинность проверяет 2.6.
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

    /// <summary>ПИН у провайдера — число (JSON number), ведущие нули теряются; приводим к 14 цифрам.</summary>
    private static string? FormatPin(long? pin) =>
        pin is > 0 ? pin.Value.ToString("D14", CultureInfo.InvariantCulture) : null;

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
