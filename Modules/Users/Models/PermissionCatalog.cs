using delosfera_server.Common.Extensions;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Справочник описаний прав — для отображения в UI администратора при создании/редактировании ролей
/// </summary>
public static class PermissionCatalog
{
    public static readonly Dictionary<PermissionCode, PermissionDescription> Descriptions = new()
    {
        [PermissionCode.ViewVndActualizationPage] = new PermissionDescription
        {
            TitleRu = "Просмотр страницы актуализации ВНД",
            TitleEn = "View the VND actualization page",
            TitleKg = "ВНДди актуалдаштыруу баракчасын көрүү"
        },
        [PermissionCode.ActualizeAnyVndWithApproval] = new PermissionDescription
        {
            TitleRu = "Возможность взять любую ВНД в актуализацию с последующим согласованием (без запроса права)",
            TitleEn = "Ability to take any VND for actualization with subsequent approval, without requesting the right",
            TitleKg = "Кийинки макулдашуу менен каалаган ВНДди актуалдаштырууга алуу мүмкүнчүлүгү (укукту суратпастан)"
        },
        [PermissionCode.ActualizeAnyVndWithoutApproval] = new PermissionDescription
        {
            TitleRu = "Возможность взять любую ВНД в актуализацию без согласования (без запроса права)",
            TitleEn = "Ability to take any VND for actualization without approval, without requesting the right",
            TitleKg = "Макулдашуусуз каалаган ВНДди актуалдаштырууга алуу мүмкүнчүлүгү (укукту суратпастан)"
        },
        [PermissionCode.ActualizeVndWithApprovalByRequest] = new PermissionDescription
        {
            TitleRu = "Возможность взять ВНД в актуализацию с последующим согласованием (по запросу права)",
            TitleEn = "Ability to take a VND for actualization with subsequent approval (by requesting the right)",
            TitleKg = "Кийинки макулдашуу менен ВНДди актуалдаштырууга алуу мүмкүнчүлүгү (укукту суроо менен)"
        },
        [PermissionCode.ActualizeVndWithoutApprovalByRequest] = new PermissionDescription
        {
            TitleRu = "Возможность взять ВНД в актуализацию без согласования (по запросу права)",
            TitleEn = "Ability to take a VND for actualization without approval (by requesting the right)",
            TitleKg = "Макулдашуусуз ВНДди актуалдаштырууга алуу мүмкүнчүлүгү (укукту суроо менен)"
        },
        [PermissionCode.CreateVndWithApproval] = new PermissionDescription
        {
            TitleRu = "Возможность создать новую ВНД с последующим согласованием",
            TitleEn = "Ability to create a new VND with subsequent approval",
            TitleKg = "Кийинки макулдашуу менен жаңы ВНД түзүү мүмкүнчүлүгү"
        },
        [PermissionCode.CreateVndWithoutApproval] = new PermissionDescription
        {
            TitleRu = "Возможность создать новую ВНД без последующего согласования",
            TitleEn = "Ability to create a new VND without subsequent approval",
            TitleKg = "Макулдашуусуз жаңы ВНД түзүү мүмкүнчүлүгү"
        },
        [PermissionCode.DeleteVnd] = new PermissionDescription
        {
            TitleRu = "Возможность удалить ВНД",
            TitleEn = "Ability to delete a VND",
            TitleKg = "ВНДди өчүрүү мүмкүнчүлүгү"
        },
        [PermissionCode.EditLastRevisionDirectly] = new PermissionDescription
        {
            TitleRu = "Возможность редактировать последнюю редакцию без согласования, без создания новой редакции и изменения даты актуализации",
            TitleEn = "Ability to edit the latest revision directly, without approval, without creating a new revision, and without changing the actualization date",
            TitleKg = "Макулдашуусуз, жаңы редакция түзбөстөн жана актуалдаштыруу датасын өзгөртпөстөн акыркы редакцияны түздөн-түз оңдоо мүмкүнчүлүгү"
        },
        [PermissionCode.ManageGroups] = new PermissionDescription
        {
            TitleRu = "Управление группами",
            TitleEn = "Manage groups",
            TitleKg = "Топторду башкаруу"
        },
        [PermissionCode.ManageDictionaries] = new PermissionDescription
        {
            TitleRu = "Управление справочниками",
            TitleEn = "Manage dictionaries",
            TitleKg = "Маалымдамаларды башкаруу"
        },
        [PermissionCode.ViewVnd] = new PermissionDescription
        {
            TitleRu = "Просмотр ВНД",
            TitleEn = "View VND",
            TitleKg = "ВНДди көрүү"
        },
        [PermissionCode.ExportVnd] = new PermissionDescription
        {
            TitleRu = "Экспорт ВНД",
            TitleEn = "Export VND",
            TitleKg = "ВНДди экспорттоо"
        },
        [PermissionCode.ManageUsers] = new PermissionDescription
        {
            TitleRu = "Управление пользователями",
            TitleEn = "Manage users",
            TitleKg = "Колдонуучуларды башкаруу"
        },
        [PermissionCode.ManageRoles] = new PermissionDescription
        {
            TitleRu = "Управление ролями",
            TitleEn = "Manage roles",
            TitleKg = "Ролдорду башкаруу"
        },
        [PermissionCode.ViewLimitedStatistics] = new PermissionDescription
        {
            TitleRu = "Просмотр ограниченной статистики",
            TitleEn = "View limited statistics",
            TitleKg = "Чектелген статистиканы көрүү"
        },
        [PermissionCode.ExportFullStatisticsReport] = new PermissionDescription
        {
            TitleRu = "Экспорт отчёта по полной статистике",
            TitleEn = "Export full statistics report",
            TitleKg = "Толук статистика боюнча отчётту экспорттоо"
        },
        [PermissionCode.ViewFullStatistics] = new PermissionDescription
        {
            TitleRu = "Просмотр полной статистики",
            TitleEn = "View full statistics",
            TitleKg = "Толук статистиканы көрүү"
        },
        [PermissionCode.ActAsApprover] = new PermissionDescription
        {
            TitleRu = "Возможность выступать в роли согласующего",
            TitleEn = "Ability to act as an approver",
            TitleKg = "Макулдаштыруучу катары иштөө мүмкүнчүлүгү"
        },
        [PermissionCode.ModifyApprovalRoute] = new PermissionDescription
        {
            TitleRu = "Возможность изменять маршрут согласования (удалять лишних пользователей)",
            TitleEn = "Ability to modify the approval route (remove unnecessary users)",
            TitleKg = "Макулдашуу маршрутун өзгөртүү мүмкүнчүлүгү (ашыкча колдонуучуларды алып салуу)"
        }
    };

    public static List<PermissionResponse> GetAll(string languageCode) =>
        Descriptions.Select(kv => new PermissionResponse
        {
            Code = (int)kv.Key,
            Key = kv.Key.ToString(),
            Description = kv.Value.ResolveTitle(languageCode)
        }).ToList();
}