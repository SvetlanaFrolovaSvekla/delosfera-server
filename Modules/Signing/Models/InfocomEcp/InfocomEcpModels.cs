using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace delosfera_server.Modules.Signing.Models.InfocomEcp;

/// <summary>
/// DTO для проксирования 6 методов ГП «Инфоком» через delosfera-ecp-service (185.88.32.25).
/// Бекенд (10.222.10.42) не обращается к cdsapi-test.srs.kg напрямую — только к ecp-service,
/// который уже добавляет Bearer/User-Agent и делает реальный вызов наружу. Поля зеркалят
/// документацию провайдера (см. InfocomEcpController) один в один.
/// </summary>

public class InfocomUserAuthMethodsRequest
{
    [RegularExpression(InfocomFieldRules.PinPattern, ErrorMessage = InfocomFieldRules.PinError)]
    public required string PersonIdnp { get; set; }

    /// <summary>Не обязателен для физ. лица (см. документацию ГП) — согласующие подписывают как
    /// физ. лица, поле больше не вводится на клиенте. Пустое значение не отправляется дальше вовсе.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationInn { get => _organizationInn; set => _organizationInn = InfocomFieldRules.NormalizeInn(value); }
    private string? _organizationInn;
}

public class InfocomGetPinCodeRequest
{
    [RegularExpression(InfocomFieldRules.PinPattern, ErrorMessage = InfocomFieldRules.PinError)]
    public required string PersonIdnp { get; set; }

    /// <summary>Не обязателен для физ. лица (см. документацию ГП) — согласующие подписывают как
    /// физ. лица, поле больше не вводится на клиенте. Пустое значение не отправляется дальше вовсе.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationInn { get => _organizationInn; set => _organizationInn = InfocomFieldRules.NormalizeInn(value); }
    private string? _organizationInn;
    public required string Method { get; set; }
}

public class InfocomAccountAuthRequest
{
    [RegularExpression(InfocomFieldRules.PinPattern, ErrorMessage = InfocomFieldRules.PinError)]
    public required string PersonIdnp { get; set; }

    /// <summary>Не обязателен для физ. лица (см. документацию ГП) — согласующие подписывают как
    /// физ. лица, поле больше не вводится на клиенте. Пустое значение не отправляется дальше вовсе.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationInn { get => _organizationInn; set => _organizationInn = InfocomFieldRules.NormalizeInn(value); }
    private string? _organizationInn;
    public required string ByPin { get; set; }
}

public class InfocomGetCertInfoRequest
{
    public required string UserToken { get; set; }
}

public class InfocomSignHashRequest
{
    public required string Hash { get; set; }
    public required string UserToken { get; set; }
}

public class InfocomCheckSignRequest
{
    public required string SignBase64 { get; set; }
    public required string Hash { get; set; }
}

/// <summary>Тело ошибки ecp-service/ГП Инфоком (errorCode/errorMessage), пробрасывается клиенту как есть.</summary>
public class InfocomErrorBody
{
    public int ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? IsActive { get; set; }
    public string? InactiveReason { get; set; }
}

/// <summary>ПИН физ. лица — ровно 14 цифр; проверяется ДО обращения к ecp-service/ГП
/// ([ApiController] сам вернёт 400), чтобы заведомо неверный ввод не расходовал попытки у
/// провайдера. Та же проверка продублирована в delosfera-ecp-service.</summary>
public static class InfocomFieldRules
{
    public const string PinPattern = @"^\d{14}$";
    public const string PinError = "ПИН физ. лица должен состоять ровно из 14 цифр";

    public static bool IsValidPin(string? value) =>
        value is { Length: 14 } && value.All(char.IsAsciiDigit);

    public static string? NormalizeInn(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Ответ 2.6 check-sign/for-hash — типизированно, для проверки на сервере (см.
/// ResolutionSignatureService). personIdnp у провайдера — число, поэтому long.</summary>
public class InfocomCheckSignResult
{
    public long? PersonIdnp { get; set; }
    public string? PersonFio { get; set; }
    public string? OrganizationInn { get; set; }
    public string? OrganizationName { get; set; }
    public bool Verified { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>Ответ 2.4 get-cert-info — типизированно (см. ResolutionSignatureService).</summary>
public class InfocomCertInfoResult
{
    public string? ValidNotBefore { get; set; }
    public string? ValidNotAfter { get; set; }
    public long? PersonIdnp { get; set; }
    public string? CommonName { get; set; }
    public string? OrganizationInn { get; set; }
    public string? OrganizationName { get; set; }
    public string? KeyIdentifier { get; set; }
    public InfocomCertIssuer? CertIssuer { get; set; }
}

public class InfocomCertIssuer
{
    public string? CommonName { get; set; }
    public string? OrganizationName { get; set; }
    public string? CountryName { get; set; }
}
