using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Запись журнала подписей ЭЦП по служебной записке (GET /api/sz/{id}/ecp/signatures). Сама
/// CMS-подпись не отдаётся — только её SHA-256; полная подпись остаётся в БД как доказательство.</summary>
public class SzResolutionSignatureResponse
{
    public int Id { get; set; }

    /// <summary>Участник маршрута — по нему карточка ставит штамп у нужной визы.</summary>
    public int RouteParticipantId { get; set; }
    public int RouteInstanceId { get; set; }

    /// <summary>Подпись относится к текущему маршруту записки (а не к прошлому кругу согласования).</summary>
    public bool IsCurrentRoute { get; set; }
    public int StepOrder { get; set; }

    public ResolutionType Decision { get; set; }

    public int UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? PersonFio { get; set; }
    public string? OrganizationName { get; set; }
    public string? CertCommonName { get; set; }
    public string? CertIssuerCommonName { get; set; }
    public string? CertValidNotAfter { get; set; }

    public required string HashSchemaVersion { get; set; }
    public required string Hash { get; set; }
    public required string SignatureSha256 { get; set; }

    /// <summary>Подпись под текущей версией содержимого записки.</summary>
    public bool MatchesCurrentContent { get; set; }

    public DateTime SignedAt { get; set; }
    public DateTime VerifiedAt { get; set; }
    public bool Verified { get; set; }

    /// <summary>Тестовая имитация ЭЦП (EcpSigning:AllowSzTestEcp) — не подпись.</summary>
    public bool IsTest { get; set; }
}
