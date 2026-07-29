namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Способ сортировки списка групп пользователей
/// </summary>
public enum UserGroupSortBy
{
    /// <summary>По дате создания, от старых к новым</summary>
    CreatedAtAsc,

    /// <summary>По дате создания, от новых к старым</summary>
    CreatedAtDesc,

    /// <summary>По названию, А → Я</summary>
    NameAsc,

    /// <summary>По названию, Я → А</summary>
    NameDesc
}