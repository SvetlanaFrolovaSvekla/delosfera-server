using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Search.DTO;

/// <summary>Где искать. Пустой список — во всём, что доступно сотруднику.</summary>
public enum SearchScope
{
    Sz = 1,
    Procurement = 2,
    Contract = 3,
    Meeting = 4,
}

public class SearchRequest
{
    /// <summary>Строка поиска. Пусто — выборка только по реквизитам (атрибутивный поиск).</summary>
    public string? Query { get; set; }

    public List<SearchScope> Scopes { get; set; } = [];

    /// <summary>Статус карточки — коды свои у каждого контура.</summary>
    public List<string> Statuses { get; set; } = [];

    public int? AuthorId { get; set; }
    public int? OrgUnitId { get; set; }

    /// <summary>Период по дате создания карточки.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Диапазон суммы — применим к заявкам и договорам.</summary>
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchHitDto
{
    public SearchScope Scope { get; set; }
    public string ScopeTitle { get; set; } = string.Empty;

    /// <summary>Идентификатор карточки своего контура — по нему строится ссылка.</summary>
    public int Id { get; set; }

    public string? RegNumber { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Фрагмент, в котором нашлось совпадение.</summary>
    public string? Snippet { get; set; }

    public string? StatusTitle { get; set; }
    public string? AuthorName { get; set; }
    public string? OrgUnitTitle { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class SearchResultDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SearchHitDto> Items { get; set; } = [];

    /// <summary>Сколько нашлось в каждом контуре — по ним строятся вкладки в интерфейсе.</summary>
    public Dictionary<string, int> CountByScope { get; set; } = [];
}

public class SavedSearchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SearchRequest Criteria { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SaveSearchRequest
{
    public required string Name { get; set; }
    public required SearchRequest Criteria { get; set; }
}

/// <summary>Тип документа контура — для ссылок и подписей.</summary>
public static class SearchScopeMap
{
    public static string Title(SearchScope scope) => scope switch
    {
        SearchScope.Sz => "Служебные записки",
        SearchScope.Procurement => "Закупки",
        SearchScope.Contract => "Договоры",
        SearchScope.Meeting => "Заседания",
        _ => scope.ToString(),
    };

    public static DocumentType? DocumentType(SearchScope scope) => scope switch
    {
        SearchScope.Sz => Documents.Models.DocumentType.Sz,
        SearchScope.Procurement => Documents.Models.DocumentType.Procurement,
        SearchScope.Contract => Documents.Models.DocumentType.Contract,
        _ => null,
    };
}
