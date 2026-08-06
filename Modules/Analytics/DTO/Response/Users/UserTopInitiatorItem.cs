namespace delosfera_server.Modules.Analytics.DTO.Response.Users;

/// <summary>Пользователь-инициатор ВНД и статистика по созданным им документам — связка модулей
/// "Пользователи" и "ВНД" (кто больше всех создаёт/актуализирует документы)</summary>
public class UserTopInitiatorItem
{
    /// <summary>Id пользователя</summary>
    public int UserId { get; set; }

    /// <summary>ФИО</summary>
    public required string FullName { get; set; }

    /// <summary>Подразделение пользователя</summary>
    public string? OrgUnitLabel { get; set; }

    /// <summary>Всего ВНД, созданных этим пользователем (как инициатор)</summary>
    public int VndCreatedCount { get; set; }

    /// <summary>Из них уже действующих</summary>
    public int VndActiveCount { get; set; }

    /// <summary>Циклов актуализации, за которые пользователь был ответственным</summary>
    public int ActualizationCyclesCount { get; set; }
}
