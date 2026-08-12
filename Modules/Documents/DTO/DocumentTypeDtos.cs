using System.Text.Json;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.DTO;

// ── Настраиваемые типы документов (GEN-06) ───────────────────────────────────

public class DocumentTypeSaveRequest
{
    /// <summary>Задаётся при создании; при изменении не меняется — на код ссылаются настройки.</summary>
    public string? Code { get; set; }

    public string? TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Description { get; set; }
    public int? RouteTemplateId { get; set; }
    public string? NumberPattern { get; set; }
    public bool? IsActive { get; set; }
}

public class DocumentTypeFieldRequest
{
    public string? Code { get; set; }
    public string? TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public CustomFieldKind Kind { get; set; }
    public int? DictionaryId { get; set; }
    public bool IsRequired { get; set; }
    public bool ShowInList { get; set; }
    public int? Order { get; set; }
    public string? Hint { get; set; }
}

public class DocumentTypeFieldDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public CustomFieldKind Kind { get; set; }
    public string KindTitle { get; set; } = string.Empty;
    public int? DictionaryId { get; set; }
    public string? DictionaryTitle { get; set; }
    public bool IsRequired { get; set; }
    public bool ShowInList { get; set; }
    public int Order { get; set; }
    public string? Hint { get; set; }
}

public class DocumentTypeDefinitionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Description { get; set; }
    public int? RouteTemplateId { get; set; }
    public string? RouteTemplateName { get; set; }
    public string? NumberPattern { get; set; }
    public bool IsActive { get; set; }
    public List<DocumentTypeFieldDto> Fields { get; set; } = [];
}

// ── Карточки по настраиваемому типу ──────────────────────────────────────────

public class CustomDocumentSaveRequest
{
    public int DefinitionId { get; set; }
    public string? Title { get; set; }

    /// <summary>Значения полей по их кодам.</summary>
    public Dictionary<string, JsonElement> Values { get; set; } = [];
}

public class CustomDocumentDto
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string TypeTitle { get; set; } = string.Empty;
    public string? RegNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Значения полей карточки по кодам.</summary>
    public Dictionary<string, JsonElement> Values { get; set; } = [];
}

// ── Представления журналов (GEN-10) ──────────────────────────────────────────

public class ListViewSaveRequest
{
    public required string Scope { get; set; }
    public required string Name { get; set; }
    public List<string> Columns { get; set; } = [];
    public JsonElement? Filter { get; set; }

    /// <summary>Сделать представление общим для банка — доступно администратору.</summary>
    public bool IsShared { get; set; }

    public bool IsDefault { get; set; }
}

public class ListViewDto
{
    public int Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public JsonElement? Filter { get; set; }
    public bool IsShared { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>Своё представление сотрудник может изменить, общее — только администратор.</summary>
    public bool CanEdit { get; set; }
}
