namespace delosfera_server.Modules.Coauthoring.DTO;

/// <summary>
/// Конфиг редактора OnlyOffice (COOP-1). Структура повторяет объект, который DocsAPI
/// (api.js) принимает в DocEditor: см. document / editorConfig / permissions.
/// Отдаётся клиенту как есть; <see cref="Token"/> — подпись всего объекта секретом DS.
/// </summary>
public sealed class OnlyOfficeConfigResponse
{
    /// <summary>Адрес Document Server — с него клиент грузит api.js.</summary>
    public required string ServerUrl { get; set; }

    /// <summary>Тип документа для api.js: "word" | "cell" | "slide".</summary>
    public required string DocumentType { get; set; }

    public required OnlyOfficeDocument Document { get; set; }
    public required OnlyOfficeEditorConfig EditorConfig { get; set; }

    /// <summary>JWT всего конфига (HS256 секретом OnlyOffice:JwtSecret).</summary>
    public required string Token { get; set; }
}

public sealed class OnlyOfficeDocument
{
    /// <summary>Расширение без точки: docx / xlsx / pptx.</summary>
    public required string FileType { get; set; }

    /// <summary>
    /// Ключ документа: версия + хеш. Меняется при каждой новой версии — так DS
    /// понимает, что кэш устарел, и перечитывает файл вместо показа старого.
    /// </summary>
    public required string Key { get; set; }

    public required string Title { get; set; }

    /// <summary>Временная подписанная ссылка, по которой DS забирает файл из MinIO (через наш API).</summary>
    public required string Url { get; set; }

    public required OnlyOfficePermissions Permissions { get; set; }
}

/// <summary>Права в редакторе: edit — правка, review — рецензирование, comment — комментарии.</summary>
public sealed class OnlyOfficePermissions
{
    public bool Edit { get; set; }
    public bool Review { get; set; }
    public bool Comment { get; set; }
    public bool Download { get; set; } = true;
    public bool Print { get; set; } = true;
}

public sealed class OnlyOfficeEditorConfig
{
    /// <summary>edit | review | view — стартовый режим редактора.</summary>
    public required string Mode { get; set; }

    /// <summary>Куда DS шлёт результат правок (наш /api/coauthoring/callback).</summary>
    public required string CallbackUrl { get; set; }

    public string Lang { get; set; } = "ru";

    public required OnlyOfficeUser User { get; set; }
}

public sealed class OnlyOfficeUser
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}
