using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Журнал подписей ЭЦП ГП «Инфоком» под резолюциями "Согласовать" (SIG-ECP) — доказательная
/// запись "кто, чем, что именно и когда подписал". Одна строка на одну подписанную резолюцию.
///
/// НЕИЗМЕНЯЕМАЯ (append-only): после вставки строка не обновляется и не удаляется — ни через API
/// (эндпоинтов изменения нет), ни через EF (DelosferaDbContext.ApplyAuditInfo бросает исключение
/// на Modified/Deleted), ни напрямую в БД (триггер из миграции AddVndResolutionSignatures).
/// Все FK — Restrict: документ/процесс/этап/редакцию с подписями удалить нельзя.
///
/// Что сюда НЕ пишется намеренно: токен доступа Инфокома (userToken 2.3, EcpCertToken из запроса —
/// используется только "на лету" для 2.4), пин-код, Bearer-токены. Полный текст документа тоже
/// не хранится — вместо канонической строки хэша хранятся её компоненты по отдельности (см.
/// ResolutionSignatureHasher): комментарий как есть + его хэш, отпечаток содержимого редакции
/// (SHA-256 файлов), код ВНД как есть. Так при повторной проверке видно, какая именно часть
/// разошлась с подписанным.
/// </summary>
public class VndResolutionSignature
{
    public int Id { get; set; }

    // --- К чему относится подпись
    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }

    /// <summary>Редакция, которую согласовывали (process.RedactionId на момент подписи) — по ней
    /// же пересчитывается хэш содержимого при повторной проверке.</summary>
    public int RedactionId { get; set; }
    public VndRedaction? Redaction { get; set; }

    /// <summary>Этап согласования. Отдельной записи "решение" в модели нет — решение хранится
    /// полями этапа по фазам (Primary*/Repeat*/FinalHold*), поэтому резолюция однозначно
    /// определяется тройкой (StageId, Phase, RoundNumber).</summary>
    public int StageId { get; set; }
    public VndApprovalStage? Stage { get; set; }

    public ApprovalStagePhase Phase { get; set; }

    /// <summary>Номер круга внутри фазы (1 для Primary; для Repeat/FinalHold — число уже
    /// сохранённых VndApprovalPhaseRound этой фазы + 1, как в DetermineActiveRevisionPhaseAsync).</summary>
    public int RoundNumber { get; set; }

    public ApprovalStageDecision Decision { get; set; }

    // --- Чем подписано
    /// <summary>ЭЦП ГП «Инфоком» (Түндүк) или локальная подпись системы «Делосфера» (право
    /// SignWithoutNationalEcp — сотрудники без ЭЦП КР). Для DelosferaSystem: PersonIdnp пуст (ПИН
    /// нет), SignatureBase64 — HMAC-SHA256 печать системы (см. ResolutionSignatureService
    /// .SystemSealPayload), CertKeyIdentifier — идентификатор ключа печати; ГП не вызывается.</summary>
    public ResolutionSignatureMethod Method { get; set; } = ResolutionSignatureMethod.InfocomEcp;

    // --- Кто подписал
    /// <summary>Пользователь delosfera, который отправил резолюцию.</summary>
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>ПИН владельца подписи ИЗ ОТВЕТА 2.6 check-sign (не из того, что ввёл пользователь).</summary>
    public required string PersonIdnp { get; set; }
    public string? PersonFio { get; set; }
    public string? OrganizationInn { get; set; }
    public string? OrganizationName { get; set; }

    // --- Сертификат (2.4 get-cert-info) — best-effort, может отсутствовать
    public string? CertKeyIdentifier { get; set; }
    public string? CertCommonName { get; set; }
    public string? CertValidNotBefore { get; set; }
    public string? CertValidNotAfter { get; set; }
    public string? CertIssuerCommonName { get; set; }
    public string? CertIssuerOrganizationName { get; set; }

    // --- Что подписано
    /// <summary>Версия схемы канонической строки (ResolutionSignatureHasher.SchemaVersion).</summary>
    public required string HashSchemaVersion { get; set; }

    /// <summary>SHA-256 канонической строки (hex upper-case) — ровно то, что уходило в 2.5.</summary>
    public required string Hash { get; set; }

    /// <summary>Комментарий резолюции в нормализованном виде (как он вошёл в хэш).</summary>
    public required string CommentText { get; set; }

    /// <summary>SHA-256 нормализованного комментария (hex upper-case).</summary>
    public required string CommentHash { get; set; }

    /// <summary>Компонент "content" канонической строки: SHA-256 файлов текста редакции по языкам
    /// ("ru=HEX;kg=HEX|-;en=HEX|-"). Сам текст документа не хранится.</summary>
    public required string ContentDigest { get; set; }

    /// <summary>Код ВНД (VndDocument.Code) на момент подписи — как есть.</summary>
    public required string VndCode { get; set; }

    /// <summary>CMS/PKCS#7-подпись целиком (base64) — не секрет, а доказательство.</summary>
    public required string SignatureBase64 { get; set; }

    /// <summary>SHA-256 байтов подписи (hex upper-case), уникальный индекс: одну и ту же подпись
    /// нельзя предъявить повторно (защита от replay).</summary>
    public required string SignatureSha256 { get; set; }

    // --- Когда и результат проверки
    /// <summary>Серверное время сохранения резолюции.</summary>
    public DateTime SignedAt { get; set; }

    /// <summary>timestamp из ответа 2.5 (как его прислал клиент, Unix ms → UTC) — справочно,
    /// клиентскому значению не доверяем (см. CmsSigningTime).</summary>
    public DateTime? ProviderTimestamp { get; set; }

    /// <summary>signingTime из подписанных атрибутов самой CMS-подписи, если провайдер его
    /// проставляет — единственное время подписи, которое нельзя подделать на клиенте.</summary>
    public DateTime? CmsSigningTime { get; set; }

    /// <summary>Момент проверки 2.6 при сохранении.</summary>
    public DateTime VerifiedAt { get; set; }

    /// <summary>timestamp из ответа 2.6 (время операции проверки у провайдера).</summary>
    public DateTime? ProviderCheckTimestamp { get; set; }

    /// <summary>Результат 2.6 на момент сохранения. Резолюции с Verified=false не сохраняются
    /// вовсе (fail closed), поле хранится для полноты доказательной записи.</summary>
    public bool Verified { get; set; }
    public string? VerificationError { get; set; }

    // --- Откуда
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class VndResolutionSignatureConfiguration : IEntityTypeConfiguration<VndResolutionSignature>
{
    public void Configure(EntityTypeBuilder<VndResolutionSignature> b)
    {
        b.ToTable("vnd_resolution_signature");
        b.HasKey(x => x.Id).HasName("pk_vnd_resolution_signature");

        b.Property(x => x.PersonIdnp).HasMaxLength(14);
        b.Property(x => x.Method).HasDefaultValue(ResolutionSignatureMethod.InfocomEcp);
        b.Property(x => x.PersonFio).HasMaxLength(300);
        b.Property(x => x.OrganizationInn).HasMaxLength(20);
        b.Property(x => x.OrganizationName).HasMaxLength(500);
        b.Property(x => x.CertKeyIdentifier).HasMaxLength(200);
        b.Property(x => x.CertCommonName).HasMaxLength(300);
        b.Property(x => x.CertValidNotBefore).HasMaxLength(64);
        b.Property(x => x.CertValidNotAfter).HasMaxLength(64);
        b.Property(x => x.CertIssuerCommonName).HasMaxLength(300);
        b.Property(x => x.CertIssuerOrganizationName).HasMaxLength(500);
        b.Property(x => x.HashSchemaVersion).HasMaxLength(16);
        b.Property(x => x.Hash).HasMaxLength(64);
        b.Property(x => x.CommentHash).HasMaxLength(64);
        b.Property(x => x.ContentDigest).HasMaxLength(256);
        b.Property(x => x.VndCode).HasMaxLength(100);
        b.Property(x => x.SignatureBase64).HasColumnName("signature_base64");
        b.Property(x => x.SignatureSha256).HasColumnName("signature_sha256").HasMaxLength(64);
        b.Property(x => x.VerificationError).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);

        // Одна подпись на одну резолюцию одного пользователя (этап + фаза + круг).
        b.HasIndex(x => new { x.StageId, x.Phase, x.RoundNumber, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_vnd_resolution_signature_resolution_user");

        // Повторно предъявить ту же самую подпись нельзя (replay).
        b.HasIndex(x => x.SignatureSha256)
            .IsUnique()
            .HasDatabaseName("ux_vnd_resolution_signature_signature_sha256");

        b.HasIndex(x => x.VndId).HasDatabaseName("ix_vnd_resolution_signature_vnd_id");
        // Привязка ПИН ↔ учётка delosfera (см. ResolutionSignatureService) ищет по обоим полям.
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_vnd_resolution_signature_user_id");
        b.HasIndex(x => x.PersonIdnp).HasDatabaseName("ix_vnd_resolution_signature_person_idnp");

        b.HasOne(x => x.Vnd).WithMany().HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vnd_resolution_signature_vnd_document_vnd_id");
        b.HasOne(x => x.ApprovalProcess).WithMany().HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vnd_resolution_signature_vnd_approval_process_id");
        b.HasOne(x => x.Redaction).WithMany().HasForeignKey(x => x.RedactionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vnd_resolution_signature_vnd_redaction_redaction_id");
        b.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vnd_resolution_signature_vnd_approval_stage_stage_id");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vnd_resolution_signature_user_user_id");
    }
}
