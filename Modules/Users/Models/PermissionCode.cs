namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Права доступа в системе. Фиксированный список — новые права добавляются только разработчиками.
/// </summary>
public enum PermissionCode
{
    /// <summary>Просмотр страницы актуализации ВНД</summary>
    ViewVndActualizationPage = 1,

    /// <summary>Взять любую ВНД в актуализацию с последующим согласованием (без запроса права)</summary>
    ActualizeAnyVndWithApproval = 2,

    /// <summary>Взять любую ВНД в актуализацию без согласования (без запроса права)</summary>
    ActualizeAnyVndWithoutApproval = 3,

    /// <summary>Взять ВНД в актуализацию с последующим согласованием (по запросу права)</summary>
    ActualizeVndWithApprovalByRequest = 4,

    /// <summary>Взять ВНД в актуализацию без согласования (по запросу права)</summary>
    ActualizeVndWithoutApprovalByRequest = 5,

    /// <summary>Создать новую ВНД с последующим согласованием</summary>
    CreateVndWithApproval = 6,

    /// <summary>Создать новую ВНД без последующего согласования</summary>
    CreateVndWithoutApproval = 7,

    /// <summary>Удалить ВНД</summary>
    DeleteVnd = 8,

    /// <summary>Редактировать последнюю редакцию без согласования, без создания новой редакции и изменения даты актуализации</summary>
    EditLastRevisionDirectly = 9,

    /// <summary>Управление группами</summary>
    ManageGroups = 10,

    /// <summary>Управление справочниками</summary>
    ManageDictionaries = 11,

    /// <summary>Просмотр ВНД</summary>
    ViewVnd = 12,

    /// <summary>Экспорт ВНД</summary>
    ExportVnd = 13,

    /// <summary>Управление пользователями</summary>
    ManageUsers = 14,

    /// <summary>Управление ролями</summary>
    ManageRoles = 15,

    /// <summary>Просмотр ограниченной статистики</summary>
    ViewLimitedStatistics = 16,

    /// <summary>Экспорт отчёта по полной статистике</summary>
    ExportFullStatisticsReport = 17,

    /// <summary>Просмотр полной статистики</summary>
    ViewFullStatistics = 18,

    /// <summary>Возможность выступать в роли согласующего</summary>
    ActAsApprover = 19,

    /// <summary>Возможность изменять маршрут согласования (удалять лишних пользователей)</summary>
    ModifyApprovalRoute = 20
}