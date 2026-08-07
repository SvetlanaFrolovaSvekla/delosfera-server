namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Права доступа в системе. Фиксированный список - новые права добавляются только разработчиками.
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

    /// <summary>Просмотр ВНД</summary>
    ViewVnd = 11,

    /// <summary>Экспорт ВНД</summary>
    ExportVnd = 12,

    /// <summary>Управление пользователями</summary>
    ManageUsers = 13,

    /// <summary>Управление ролями</summary>
    ManageRoles = 14,

    /// <summary>Просмотр ограниченной статистики</summary>
    ViewLimitedStatistics = 15,

    /// <summary>Экспорт отчёта по полной статистике</summary>
    ExportFullStatisticsReport = 16,

    /// <summary>Просмотр полной статистики</summary>
    ViewFullStatistics = 17,

    /// <summary>Возможность выступать в роли согласующего</summary>
    ActAsApprover = 18,

    /// <summary>Возможность изменять маршрут согласования (удалять лишних пользователей)</summary>
    ModifyApprovalRoute = 19,

    /// <summary>Возможность просматривать черновики других пользователей</summary>
    ViewOtherUsersDrafts = 20,

    /// <summary>Управление справочниками ВНД (Виды ВНД, Уровни секретности, Группы пользователей,
    /// Рубрикатор, Обязательные участники согласования, ключевые слова)</summary>
    ManageVndDictionaries = 21,

    /// <summary>Управление общими справочниками (Органы утверждения, Структурные подразделения, Должности)</summary>
    ManageGeneralDictionaries = 22,

    /// <summary>Управление справочниками служебных записок (Категории СЗ)</summary>
    ManageSzDictionaries = 23,

    /// <summary>Управление справочниками закупок (Чёрный список контрагентов, Пороги закупок)</summary>
    ManageProcurementDictionaries = 24,

    /// <summary>Изменение реквизитов уже существующей ВНД (кнопка "Изменить реквизиты")
    /// и её связей с другими документами — в отличие от Create*, не про создание, а про
    /// редактирование метаданных того, что уже есть</summary>
    EditVndRequisites = 25
}