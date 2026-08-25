using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace delosfera_server.Modules.Integrations.OrgStructure;

// ── Что отдаёт портал ───────────────────────────────────────────────────────
// Имена полей в ответе — в змеином регистре: parent_unit_id, heads_unit.
// Разбираем с политикой имён, а не переименовываем каждое поле вручную.

public sealed class PortalUnit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>board — коллегиальный орган, division — управление, department — отдел.</summary>
    public string Kind { get; set; } = string.Empty;
    public string? KindTitle { get; set; }

    /// <summary>Подразделение, которому подчинено это. Пусто у верхних узлов.</summary>
    public int? ParentUnitId { get; set; }

    /// <summary>
    /// Куратор: подразделение подчинено человеку напрямую. Так в банке выглядит
    /// подчинение заместителю председателя, и структура это допускает.
    /// </summary>
    public PortalPerson? ParentHead { get; set; }

    public PortalPerson? Head { get; set; }
    public int StaffCount { get; set; }
}

public sealed class PortalPerson
{
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public sealed class PortalEmployee
{
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Email { get; set; }
    public PortalUnitRef? Unit { get; set; }
    public PortalPerson? Manager { get; set; }
    public PortalUnitRef? HeadsUnit { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class PortalUnitRef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
}

internal sealed class UnitsResponse
{
    [JsonPropertyName("units")]
    public List<PortalUnit> Units { get; set; } = [];
}

internal sealed class EmployeesResponse
{
    [JsonPropertyName("employees")]
    public List<PortalEmployee> Employees { get; set; } = [];

    [JsonPropertyName("found")]
    public int Found { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>Портал ответил не так, как ожидалось. Текст пригоден для администратора.</summary>
public class PortalException(string message) : Exception(message);

/// <summary>
/// Обращения к API оргструктуры портала.
///
/// Только чтение: изменить что-либо через это API нельзя, и не нужно —
/// структуру ведут в портале.
/// </summary>
public class PortalOrgClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Готовит обращение. Токен идёт заголовком: в строке запроса он осел бы
    /// в журналах прокси и в истории обращений, и портал такой способ
    /// не поддерживает намеренно.
    /// </summary>
    private HttpRequestMessage Request(string baseUrl, string token, string path)
    {
        var url = $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<T> SendAsync<T>(string baseUrl, string token, string path, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(Request(baseUrl, token, path), ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PortalException("Портал не ответил за отведённое время.");
        }
        catch (HttpRequestException e)
        {
            throw new PortalException($"Портал недоступен: {e.Message}");
        }

        // Отдельные сообщения на частые случаи: администратору важно различать
        // «токен отозвали» и «портал лежит», а по коду 401 против 502 он этого
        // не поймёт, если увидит только «ошибка обращения».
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new PortalException("Портал не принял токен: его нет, он недействителен или отозван.");

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new PortalException($"Портал не знает такого адреса: {path}. Проверьте адрес портала и версию API.");

        if (!response.IsSuccessStatusCode)
            throw new PortalException($"Портал ответил {(int)response.StatusCode}.");

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            return value ?? throw new PortalException("Портал вернул пустой ответ.");
        }
        catch (JsonException e)
        {
            throw new PortalException($"Ответ портала не разобрался: {e.Message}");
        }
    }

    /// <summary>Плоский список подразделений со связями.</summary>
    public async Task<List<PortalUnit>> GetUnitsAsync(
        string baseUrl, string token, CancellationToken ct = default)
    {
        var response = await SendAsync<UnitsResponse>(baseUrl, token, "units", ct);
        return response.Units;
    }

    /// <summary>
    /// Все сотрудники, страницами. Портал отдаёт до 500 за раз и просит
    /// не опрашивать его в цикле — один проход забирает всех и на этом кончается.
    /// </summary>
    public async Task<List<PortalEmployee>> GetEmployeesAsync(
        string baseUrl, string token, CancellationToken ct = default)
    {
        const int PageSize = 500;

        var all = new List<PortalEmployee>();
        var offset = 0;

        while (true)
        {
            var page = await SendAsync<EmployeesResponse>(
                baseUrl, token, $"employees?limit={PageSize}&offset={offset}", ct);

            all.AddRange(page.Employees);

            if (!page.HasMore || page.Employees.Count == 0) break;
            offset += page.Employees.Count;

            // Предохранитель от бесконечного круга, если портал однажды
            // начнёт отдавать has_more всегда: банк меньше этого числа.
            if (all.Count > 50_000)
                throw new PortalException("Портал отдал неправдоподобно много сотрудников — обход остановлен.");
        }

        return all;
    }

    /// <summary>
    /// Проверка связи для кнопки «Проверить». Возвращает, сколько подразделений
    /// видно, — этого достаточно, чтобы понять, что адрес и токен верны.
    /// </summary>
    public async Task<int> CheckAsync(string baseUrl, string token, CancellationToken ct = default)
    {
        var units = await GetUnitsAsync(baseUrl, token, ct);
        return units.Count;
    }
}
