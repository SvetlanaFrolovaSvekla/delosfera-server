using System.Net;
using System.Text;
using delosfera_server.Modules.Signing.Services.Oep;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Delosfera.Tests;

/// <summary>
/// Клиент API облачной ЭП ГП «Инфоком»: разбор успешных ответов, конверта ошибки и
/// поведение при выключенной интеграции. HTTP замокан — сеть не трогаем.
/// </summary>
public class OepSignatureClientTests
{
    [Fact]
    public async Task Authenticate_парсит_токен_из_ответа()
    {
        var client = Client((req, body) =>
        {
            Assert.EndsWith("/account/auth", req.RequestUri!.AbsoluteUri);
            Assert.Contains("\"byPin\":\"123456\"", body);
            return Json("{\"token\":\"eyJ.userToken\",\"personFio\":\"Иванов И.И.\",\"isActive\":true}");
        });

        var r = await client.AuthenticateAsync("20101200100111", "0123", "123456");

        Assert.Equal("eyJ.userToken", r.Token);
        Assert.Equal("Иванов И.И.", r.PersonFio);
    }

    [Fact]
    public async Task SignHash_возвращает_подпись_и_отметку()
    {
        var client = Client((req, body) =>
        {
            Assert.EndsWith("/get-sign/for-hash", req.RequestUri!.AbsoluteUri);
            Assert.Contains("\"hash\":\"ABC\"", body);
            return Json("{\"isSuccesfull\":true,\"sign\":\"MIIBASE64\",\"timestamp\":1589189879224}");
        });

        var r = await client.SignHashAsync("ABC", "eyJ.userToken");

        Assert.True(r.IsSuccessful);
        Assert.Equal("MIIBASE64", r.Sign);
        Assert.Equal(1589189879224, r.Timestamp);
    }

    [Fact]
    public async Task Конверт_ошибки_превращается_в_OepApiException()
    {
        var client = Client((_, _) =>
            Json("{\"errorCode\":403,\"errorMessage\":\"Неверный ПИН-код! Осталось попыток: 2\"}"));

        var ex = await Assert.ThrowsAsync<OepApiException>(() =>
            client.AuthenticateAsync("20101200100111", "0123", "000000"));

        Assert.Equal(403, ex.ErrorCode);
        Assert.Contains("Неверный ПИН-код", ex.Message);
    }

    [Fact]
    public async Task Выключенная_интеграция_бросает_InvalidOperation()
    {
        var client = new OepSignatureClient(
            new HttpClient(new StubHandler((_, _) => Json("{}"))),
            Options.Create(new OepOptions()),   // пустой BaseUrl/ServiceToken → Enabled == false
            NullLogger<OepSignatureClient>.Instance);

        Assert.False(client.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAuthMethodsAsync("20101200100111", "0123"));
    }

    // ── стенд ──────────────────────────────────────────────────────────────────

    private static OepSignatureClient Client(Func<HttpRequestMessage, string, HttpResponseMessage> respond) =>
        new(
            new HttpClient(new StubHandler(respond)),
            Options.Create(new OepOptions
            {
                BaseUrl = "https://cdsapi-test.srs.kg/api",
                ServiceToken = "service-token",
                OrganizationName = "Keremet Bank",
            }),
            NullLogger<OepSignatureClient>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return respond(request, body);
        }
    }
}
