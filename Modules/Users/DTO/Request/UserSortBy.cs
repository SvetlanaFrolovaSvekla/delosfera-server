namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Способ сортировки списка пользователей
/// </summary>
public enum UserSortBy
{
    /// <summary>По дате создания, от старых к новым</summary>
    CreatedAtAsc,

    /// <summary>По дате создания, от новых к старым</summary>
    CreatedAtDesc,

    /// <summary>По ФИО, А → Я</summary>
    NameAsc,

    /// <summary>По ФИО, Я → А</summary>
    NameDesc
}