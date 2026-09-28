using System.Text.Json.Serialization;

namespace delosfera_server.Modules.Signing.Services.Oep;

// Форматы запросов/ответов API ОЭП ГП «Инфоком» (спецификация v2.3, раздел 2).
// Имена полей — как в API (JSON), поэтому JsonPropertyName проставлен явно.

// ── 2.1 Методы авторизации ────────────────────────────────────────────────────

public sealed class OepAuthMethodsRequest
{
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
}

public sealed class OepAuthMethod
{
    [JsonPropertyName("authType")] public string? AuthType { get; set; }   // "email" | "sms"
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("createAt")] public string? CreateAt { get; set; }
}

public sealed class OepAuthMethodsResponse
{
    [JsonPropertyName("userAuthMethodList")] public List<OepAuthMethod> UserAuthMethodList { get; set; } = [];

    // При неактивной учётной записи API отдаёт эти поля вместо списка.
    [JsonPropertyName("isActive")] public bool? IsActive { get; set; }
    [JsonPropertyName("inactiveReason")] public string? InactiveReason { get; set; }
}

// ── 2.2 Отправка пин-кода ──────────────────────────────────────────────────────

public sealed class OepSendPinRequest
{
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
    [JsonPropertyName("method")] public string Method { get; set; } = "";   // "sms" | "email"
}

public sealed class OepSendPinResponse
{
    [JsonPropertyName("status")] public int Status { get; set; }
}

// ── 2.3 Получение токена доступа (по пин-коду) ─────────────────────────────────

public sealed class OepAuthRequest
{
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
    [JsonPropertyName("byPin")] public string ByPin { get; set; } = "";
}

public sealed class OepAuthResponse
{
    [JsonPropertyName("token")] public string? Token { get; set; }          // userToken, действует 1 минуту
    [JsonPropertyName("personFio")] public string? PersonFio { get; set; }
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
    [JsonPropertyName("organizationName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("subjectType")] public int? SubjectType { get; set; } // 0 физ / 1 предст. юр / 2 юр
    [JsonPropertyName("isActive")] public bool? IsActive { get; set; }
    [JsonPropertyName("createAt")] public string? CreateAt { get; set; }
    [JsonPropertyName("expireAt")] public string? ExpireAt { get; set; }
}

// ── 2.4 Сведения о сертификате ─────────────────────────────────────────────────

public sealed class OepCertInfoRequest
{
    [JsonPropertyName("userToken")] public string UserToken { get; set; } = "";
}

public sealed class OepCertIssuer
{
    [JsonPropertyName("commonName")] public string? CommonName { get; set; }
    [JsonPropertyName("organizationName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("countryName")] public string? CountryName { get; set; }
}

public sealed class OepCertInfoResponse
{
    [JsonPropertyName("validNotBefore")] public string? ValidNotBefore { get; set; }
    [JsonPropertyName("validNotAfter")] public string? ValidNotAfter { get; set; }
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("commonName")] public string? CommonName { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
    [JsonPropertyName("organizationName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("countryName")] public string? CountryName { get; set; }
    [JsonPropertyName("keyIdentifier")] public string? KeyIdentifier { get; set; }
    [JsonPropertyName("certIssuer")] public OepCertIssuer? CertIssuer { get; set; }
}

// ── 2.5 Подписание хэша ────────────────────────────────────────────────────────

public sealed class OepSignHashRequest
{
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
    [JsonPropertyName("userToken")] public string UserToken { get; set; } = "";
}

public sealed class OepSignHashResponse
{
    // В спецификации поле называется "isSuccesfull" (именно так, с опечаткой).
    [JsonPropertyName("isSuccesfull")] public bool IsSuccessful { get; set; }
    [JsonPropertyName("sign")] public string? Sign { get; set; }            // CMS/PKCS7, base64
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }     // Unix ms
}

// ── 2.6 Проверка подписи ───────────────────────────────────────────────────────

public sealed class OepCheckSignRequest
{
    [JsonPropertyName("signBase64")] public string SignBase64 { get; set; } = "";
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
}

public sealed class OepCheckSignResponse
{
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("personFio")] public string? PersonFio { get; set; }
    [JsonPropertyName("personIdnp")] public string? PersonIdnp { get; set; }
    [JsonPropertyName("organizationInn")] public string? OrganizationInn { get; set; }
    [JsonPropertyName("organizationName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
}

// ── Общий конверт ошибки ───────────────────────────────────────────────────────

public sealed class OepErrorResponse
{
    [JsonPropertyName("errorCode")] public int ErrorCode { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
}

/// <summary>Ошибка обращения к API ОЭП — несёт код и текст из ответа Инфокома.</summary>
public sealed class OepApiException(int errorCode, string message) : Exception(message)
{
    public int ErrorCode { get; } = errorCode;
}
