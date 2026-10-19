using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Хэш для подписи резолюции (2.5 get-sign/for-hash) — см. ResolutionSignatureHasher.</summary>
public class ResolutionHashResponse
{
    public required string Hash { get; set; }

    /// <summary>Версия схемы канонической строки (сейчас "v1").</summary>
    public required string SchemaVersion { get; set; }
}

/// <summary>Одна запись журнала подписей ЭЦП по документу (GET /api/vnd/{vndId}/signatures).
/// Сама CMS-подпись в список не отдаётся (она большая и для отображения не нужна) — только её
/// SHA-256; полная подпись остаётся в БД как доказательство.</summary>
public class VndResolutionSignatureResponse
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public int ApprovalProcessId { get; set; }
    public int RedactionId { get; set; }
    public int StageId { get; set; }
    public string? StageTitle { get; set; }
    public ApprovalStagePhase Phase { get; set; }
    public int RoundNumber { get; set; }

    public int UserId { get; set; }
    public string? UserFullName { get; set; }

    public string? PersonFio { get; set; }
    public string? OrganizationName { get; set; }

    public string? CertCommonName { get; set; }
    public string? CertIssuerCommonName { get; set; }

    public required string HashSchemaVersion { get; set; }
    public required string Hash { get; set; }
    public required string SignatureSha256 { get; set; }

    public DateTime SignedAt { get; set; }
    public DateTime VerifiedAt { get; set; }
    public bool Verified { get; set; }

    /// <summary>Тестовая имитация ЭЦП (EcpSigning:AllowVndTestEcp), а не настоящая подпись.</summary>
    public bool IsTest { get; set; }
}

/// <summary>Результат повторной проверки подписи (POST /api/vnd/{vndId}/signatures/{id}/verify).</summary>
public class ResolutionSignatureVerifyResponse
{
    /// <summary>"valid" — подпись валидна и документ не менялся; "document_changed" — подпись
    /// валидна, но текущее состояние документа/резолюции не совпадает с подписанным (см.
    /// ChangedComponents); "signature_invalid" — провайдер не подтвердил подпись или владелец не
    /// совпал; "journal_tampered" — запись журнала внутренне не сходится (кто-то правил БД в
    /// обход приложения); "provider_unavailable" — ГП недоступен, проверить подпись сейчас нельзя
    /// (сравнение компонентов при этом всё равно выполнено); "unsupported_schema" — неизвестная
    /// версия схемы хэша.</summary>
    public required string Status { get; set; }

    public required string Message { get; set; }

    /// <summary>Какие компоненты хэша разошлись с подписанными: "comment", "content", "code".</summary>
    public List<string> ChangedComponents { get; set; } = [];

    /// <summary>Результат 2.6 для сохранённой подписи; null — провайдер недоступен.</summary>
    public bool? SignatureVerified { get; set; }

    public string? SignerPersonFio { get; set; }

    public DateTime CheckedAt { get; set; }
}
