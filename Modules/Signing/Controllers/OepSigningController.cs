using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Signing.Services.Oep;

namespace delosfera_server.Modules.Signing.Controllers;

// Запросы фронта на облачное подписание (ОЭП ГП «Инфоком»). Сервис-токен и
// наименование организации — серверная конфигурация, наружу не отдаются. personIdnp
// приходит от клиента: у пользователя в системе пока нет верифицированного ИНП.
// TODO: когда ИНП появится в профиле — брать оттуда и запретить подпись за другого.

public sealed class OepAuthMethodsBody
{
    public string? PersonIdnp { get; set; }
    public string? OrganizationInn { get; set; }
}

public sealed class OepSendPinBody
{
    public string? PersonIdnp { get; set; }
    public string? OrganizationInn { get; set; }
    public string Method { get; set; } = "";
}

public sealed class OepSignBody
{
    public string? PersonIdnp { get; set; }
    public string? OrganizationInn { get; set; }
    public string ByPin { get; set; } = "";
}

/// <summary>
/// Облачная ЭП (ОЭП ГП «Инфоком»): выбор метода → пин-код → подпись отпечатка
/// документа/вложения. Закрытый ключ в облаке УЦ, на сервер не передаётся.
/// </summary>
[ApiController]
[Route("api/signing/oep")]
[Tags("Подписание · ОЭП")]
[Authorize]
public sealed class OepSigningController(IOepSigningService oep, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Включена ли облачная ЭП на этом контуре.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = oep.Enabled });

    /// <summary>2.1 Доступные методы авторизации (email/sms) и статус учётной записи.</summary>
    [HttpPost("auth-methods")]
    public Task<IActionResult> AuthMethods([FromBody] OepAuthMethodsBody b, CancellationToken ct) =>
        Run(() => oep.GetAuthMethodsAsync(b.PersonIdnp, b.OrganizationInn, ct));

    /// <summary>2.2 Отправить пин-код выбранным методом.</summary>
    [HttpPost("send-pin")]
    public Task<IActionResult> SendPin([FromBody] OepSendPinBody b, CancellationToken ct) =>
        Run(() => oep.SendPinAsync(b.PersonIdnp, b.OrganizationInn, b.Method, ct));

    /// <summary>Подписать карточку документа облачной ЭП по пин-коду.</summary>
    [HttpPost("documents/{documentId:int}/sign")]
    public Task<IActionResult> SignDocument(int documentId, [FromBody] OepSignBody b, CancellationToken ct) =>
        Run(() => oep.SignDocumentAsync(documentId, ToParams(b), currentUser.UserId, ct));

    /// <summary>Подписать вложение облачной ЭП по пин-коду.</summary>
    [HttpPost("attachments/{attachmentId:int}/sign")]
    public Task<IActionResult> SignAttachment(int attachmentId, [FromBody] OepSignBody b, CancellationToken ct) =>
        Run(() => oep.SignAttachmentAsync(attachmentId, ToParams(b), currentUser.UserId, ct));

    private static OepSignParams ToParams(OepSignBody b) =>
        new() { PersonIdnp = b.PersonIdnp, OrganizationInn = b.OrganizationInn, ByPin = b.ByPin };

    // Ошибку API ОЭП отдаём как 400 с её кодом/текстом, чтобы фронт показал причину
    // («Неверный ПИН-код! Осталось попыток: 2» и т. п.). Остальное — общему middleware.
    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (OepApiException ex) { return BadRequest(new { message = ex.Message, errorCode = ex.ErrorCode }); }
    }
}
