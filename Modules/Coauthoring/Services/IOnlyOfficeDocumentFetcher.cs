namespace delosfera_server.Modules.Coauthoring.Services;

/// <summary>
/// Скачивает отредактированный файл по ссылке из callback'а OnlyOffice (COOP-1).
/// Вынесено интерфейсом, чтобы в тестах подменять HTTP заглушкой, а не поднимать
/// Document Server.
/// </summary>
public interface IOnlyOfficeDocumentFetcher
{
    Task<byte[]> FetchAsync(string url, CancellationToken ct = default);
}

public sealed class HttpOnlyOfficeDocumentFetcher : IOnlyOfficeDocumentFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpOnlyOfficeDocumentFetcher(IHttpClientFactory httpClientFactory) =>
        _httpClientFactory = httpClientFactory;

    public async Task<byte[]> FetchAsync(string url, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpOnlyOfficeDocumentFetcher));
        return await client.GetByteArrayAsync(url, ct);
    }
}
