using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Журнал подписей ЭЦП ГП «Инфоком» под резолюциями согласующих служебной записки — то же, что
/// VndResolutionSignature для ВНД: доказательная запись "кто, чем, что именно и когда подписал".
/// Одна строка на одну подписанную резолюцию (участник маршрута выносит её один раз).
///
/// НЕИЗМЕНЯЕМАЯ (append-only): после вставки строка не обновляется и не удаляется — ни через API,
/// ни через EF (DelosferaDbContext.ApplyAuditInfo бросает исключение на Modified/Deleted), ни
/// напрямую в БД (триггер из миграции AddSzResolutionSignatures). Все FK — Restrict.
///
/// Рядом с записью журнала создаётся обычная подпись контура (Signing.Signature, уровень
/// Qualified) — на неё ссылается Resolution.SignatureId, поэтому маршрут, карточка и печатная
/// форма видят ЭЦП так же, как любую другую подпись. Здесь лежат подробности, которых в общей
/// подписи нет: ПИН и ФИО из ответа провайдера, сертификат, сама CMS-подпись и компоненты хэша.
///
/// Токен доступа Инфокома, пин-код и Bearer-токены сюда НЕ пишутся.
/// </summary>
public class SzResolutionSignature
{
    public int Id { get; set; }

    // --- К чему относится подпись
    public int SzDocumentId { get; set; }
    public SzDocument? SzDocument { get; set; }

    /// <summary>Единая карточка документа записки (SzDocument.DocumentId на момент подписи).</summary>
    public int DocumentId { get; set; }

    /// <summary>Участник маршрута, чью резолюцию подписали.</summary>
    public int RouteParticipantId { get; set; }
    public RouteParticipant? RouteParticipant { get; set; }

    /// <summary>Общая подпись контура (Signing.Signature), которой закрыта резолюция.</summary>
    public int SignatureId { get; set; }
    public Signature? Signature { get; set; }

    public ResolutionType Decision { get; set; }

    // --- Кто подписал
    /// <summary>Пользователь delosfera, который вынес резолюцию.</summary>
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

    // --- Что подписано (см. SzResolutionSignatureHasher)
    public required string HashSchemaVersion { get; set; }

    /// <summary>SHA-256 канонической строки (hex upper-case) — ровно то, что уходило в 2.5.</summary>
    public required string Hash { get; set; }

    /// <summary>Комментарий резолюции в нормализованном виде (как он вошёл в хэш).</summary>
    public required string CommentText { get; set; }

    /// <summary>SHA-256 нормализованного комментария (hex upper-case).</summary>
    public required string CommentHash { get; set; }

    /// <summary>Отпечаток содержимого записки (SHA-256 существенных полей и вложений, hex upper-case).
    /// Сам текст записки не хранится.</summary>
    public required string ContentDigest { get; set; }

    /// <summary>CMS/PKCS#7-подпись целиком (base64) — не секрет, а доказательство.</summary>
    public required string SignatureBase64 { get; set; }

    /// <summary>SHA-256 байтов подписи (hex upper-case), уникальный индекс — защита от replay.</summary>
    public required string SignatureSha256 { get; set; }

    // --- Когда и результат проверки
    public DateTime SignedAt { get; set; }
    public DateTime? ProviderTimestamp { get; set; }
    public DateTime? CmsSigningTime { get; set; }
    public DateTime VerifiedAt { get; set; }
    public DateTime? ProviderCheckTimestamp { get; set; }
    public bool Verified { get; set; }
    public string? VerificationError { get; set; }

    // --- Откуда
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class SzResolutionSignatureConfiguration : IEntityTypeConfiguration<SzResolutionSignature>
{
    public void Configure(EntityTypeBuilder<SzResolutionSignature> b)
    {
        b.ToTable("sz_resolution_signature");
        b.HasKey(x => x.Id).HasName("pk_sz_resolution_signature");

        // Как Resolution.Type в маршруте — строкой, чтобы журнал читался без справочника.
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.PersonIdnp).HasMaxLength(14);
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
        b.Property(x => x.ContentDigest).HasMaxLength(64);
        b.Property(x => x.SignatureBase64).HasColumnName("signature_base64");
        b.Property(x => x.SignatureSha256).HasColumnName("signature_sha256").HasMaxLength(64);
        b.Property(x => x.VerificationError).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);

        // Резолюцию участник выносит один раз — и подпись под ней одна.
        b.HasIndex(x => x.RouteParticipantId)
            .IsUnique()
            .HasDatabaseName("ux_sz_resolution_signature_route_participant_id");

        // Повторно предъявить ту же самую подпись нельзя (replay).
        b.HasIndex(x => x.SignatureSha256)
            .IsUnique()
            .HasDatabaseName("ux_sz_resolution_signature_signature_sha256");

        b.HasIndex(x => x.SignatureId).HasDatabaseName("ix_sz_resolution_signature_signature_id");
        b.HasIndex(x => x.SzDocumentId).HasDatabaseName("ix_sz_resolution_signature_sz_document_id");
        // Привязка ПИН ↔ учётка delosfera ищет по обоим полям.
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_sz_resolution_signature_user_id");
        b.HasIndex(x => x.PersonIdnp).HasDatabaseName("ix_sz_resolution_signature_person_idnp");

        b.HasOne(x => x.SzDocument).WithMany().HasForeignKey(x => x.SzDocumentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sz_resolution_signature_sz_document_sz_document_id");
        b.HasOne(x => x.RouteParticipant).WithMany().HasForeignKey(x => x.RouteParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sz_resolution_signature_route_participant_route_participant_id");
        b.HasOne(x => x.Signature).WithMany().HasForeignKey(x => x.SignatureId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sz_resolution_signature_signature_signature_id");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sz_resolution_signature_user_user_id");
    }
}
