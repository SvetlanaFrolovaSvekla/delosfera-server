using System.Text.Json.Serialization;

namespace delosfera_server.Modules.Coauthoring.DTO;

/// <summary>
/// Тело callback'а от OnlyOffice Document Server (COOP-1).
/// Значимые status: 1 — редактируется, 2 — готов к сохранению, 3 — ошибка сохранения,
/// 4 — закрыт без изменений, 6 — force-save, 7 — ошибка force-save.
/// </summary>
public sealed class OnlyOfficeCallbackRequest
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>Ссылка на отредактированный файл — по ней его и скачиваем на status=2/6.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Ключ документа, выданный в конфиге.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>Id пользователей, правивших документ (в наших id — как заданы в конфиге).</summary>
    [JsonPropertyName("users")]
    public List<string>? Users { get; set; }

    /// <summary>JWT-подпись тела (когда DS настроен с секретом); валидируется до сохранения.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
