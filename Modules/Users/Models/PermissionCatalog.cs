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
        },
        [PermissionCode.ViewOtherUsersDrafts] = new PermissionDescription
        {
            TitleRu = "Возможность просматривать черновики других пользователей",
            TitleEn = "Ability to view other users' drafts",
            TitleKg = "Башка колдонуучулардын черновиктерин көрүү мүмкүнчүлүгү"
        },
        [PermissionCode.ManageVndDictionaries] = new PermissionDescription
        {
            TitleRu = "Управление справочниками ВНД",
            TitleEn = "Manage VND dictionaries",
            TitleKg = "ВНД маалымдамаларын башкаруу"
        },
        [PermissionCode.ManageGeneralDictionaries] = new PermissionDescription
        {
            TitleRu = "Управление общими справочниками",
            TitleEn = "Manage general dictionaries",
            TitleKg = "Жалпы маалымдамаларды башкаруу"
        },
        [PermissionCode.ManageSzDictionaries] = new PermissionDescription
        {
            TitleRu = "Управление справочниками служебных записок",
            TitleEn = "Manage memo dictionaries",
            TitleKg = "Кызматтык каттардын маалымдамаларын башкаруу"
        },
        [PermissionCode.ManageProcurementDictionaries] = new PermissionDescription
        {
            TitleRu = "Управление справочниками закупок",
            TitleEn = "Manage procurement dictionaries",
            TitleKg = "Сатып алуулардын маалымдамаларын башкаруу"
        },
        [PermissionCode.EditVndRequisites] = new PermissionDescription
        {
            TitleRu = "Изменение реквизитов существующей ВНД и её связей с другими документами",
            TitleEn = "Edit requisites of an existing VND and its links to other documents",
            TitleKg = "Учурдагы ВНДдин реквизиттерин жана башка документтер менен байланышын өзгөртүү"
        },
        [PermissionCode.ViewMeetings] = new PermissionDescription
        {
            TitleRu = "Просмотр журнала заседаний Правления, КПА и комитетов",
            TitleEn = "View the register of Board, PAC and committee meetings",
            TitleKg = "Башкармалыктын, КПАнын жана комитеттердин жыйналыштарынын журналын көрүү"
        },
        [PermissionCode.ManageBoardMeetings] = new PermissionDescription
        {
            TitleRu = "Ведение заседаний Правления (Секретарь Правления)",
            TitleEn = "Maintain Board meetings (Board Secretary)",
            TitleKg = "Башкармалыктын жыйналыштарын жүргүзүү (Башкармалыктын катчысы)"
        },
        [PermissionCode.ManageKpaMeetings] = new PermissionDescription
        {
            TitleRu = "Ведение заседаний Комитета по проблемным активам (Секретарь КПА)",
            TitleEn = "Maintain Problem Assets Committee meetings (PAC Secretary)",
            TitleKg = "Көйгөйлүү активдер боюнча комитеттин жыйналыштарын жүргүзүү (КПА катчысы)"
        },
        [PermissionCode.ManageCreditCommitteeMeetings] = new PermissionDescription
        {
            TitleRu = "Ведение заседаний Кредитного комитета",
            TitleEn = "Maintain Credit Committee meetings",
            TitleKg = "Кредиттик комитеттин жыйналыштарын жүргүзүү"
        },
        [PermissionCode.ReportMeetingExecution] = new PermissionDescription
        {
            TitleRu = "Заполнение отчёта об исполнении и статуса поручений по протоколам",
            TitleEn = "Fill in execution reports and statuses for protocol assignments",
            TitleKg = "Протоколдор боюнча тапшырмалардын аткарылышы жөнүндө отчётту жана статусту толтуруу"
        },
        [PermissionCode.ExportMeetingRegistry] = new PermissionDescription
        {
            TitleRu = "Выгрузка реестра решений комитетов в Excel",
            TitleEn = "Export the committee decisions register to Excel",
            TitleKg = "Комитеттердин чечимдеринин реестрин Excelге жүктөө"
        },
        [PermissionCode.MemberOfBoard] = new PermissionDescription
        {
            TitleRu = "Член Правления — полная повестка заседаний Правления",
            TitleEn = "Board member — full agenda of Board meetings",
            TitleKg = "Башкармалыктын мүчөсү — Башкармалыктын жыйналыштарынын толук күн тартиби"
        },
        [PermissionCode.MemberOfKpa] = new PermissionDescription
        {
            TitleRu = "Член КПА — полная повестка заседаний Комитета по проблемным активам",
            TitleEn = "PAC member — full agenda of Problem Assets Committee meetings",
            TitleKg = "КПА мүчөсү — комитеттин жыйналыштарынын толук күн тартиби"
        },
        [PermissionCode.MemberOfCreditCommittee] = new PermissionDescription
        {
            TitleRu = "Член Кредитного комитета — полная повестка заседаний КИТ",
            TitleEn = "Credit Committee member — full agenda of Credit Committee meetings",
            TitleKg = "Кредиттик комитеттин мүчөсү — жыйналыштардын толук күн тартиби"
        },
        [PermissionCode.ViewVndRegistryExtended] = new PermissionDescription
        {
            TitleRu = "Просмотр реестра ВНД в расширенном режиме: статус последней редакции, актуализация",
            TitleEn = "View the VND registry in extended mode: latest revision status, actualization",
            TitleKg = "ВНД реестрин кеңейтилген режимде көрүү: акыркы редакциянын статусу, актуалдаштыруу"
        },
        [PermissionCode.CancelAnyVndApproval] = new PermissionDescription
        {
            TitleRu = "Отозвать согласование любой ВНД, не будучи инициатором (главный редактор)",
            TitleEn = "Withdraw approval of any VND without being its initiator (chief editor)",
            TitleKg = "Демилгечиси болбосо да, каалаган ВНДдин макулдашуусун артка алуу (башкы редактор)"
        },
        [PermissionCode.ConsolidateAnyVnd] = new PermissionDescription
        {
            TitleRu = "Консолидировать согласованную редакцию любой ВНД, не будучи ответственным за актуализацию или инициатором (главный методолог)",
            TitleEn = "Consolidate any VND's approved revision without being the actualization owner or the initiator (chief methodologist)",
            TitleKg = "Актуалдаштырууга жооптуу же демилгечи болбосо да, каалаган ВНДдин макулдашылган редакциясын консолидациялоо (башкы методолог)"
        },
        [PermissionCode.CancelVnd] = new PermissionDescription
        {
            TitleRu = "Архивировать (отменить) ВНД — на любом статусе, кроме черновика и уже архивированного",
            TitleEn = "Archive (cancel) a VND — at any status except draft and already archived",
            TitleKg = "ВНДди архивдөө (жокко чыгаруу) — черновиктен жана мурунтан эле архивделгенден башка бардык статуста"
        },

        // Системные настройки
        [PermissionCode.ManageSystemSettings] = new PermissionDescription
        {
            TitleRu = "Системные настройки и интеграции (шаблоны маршрутов, справочники подписи, состав органов)",
            TitleEn = "System settings and integrations (route templates, signing dictionaries, body composition)",
            TitleKg = "Системалык жөндөөлөр жана интеграциялар"
        },

        // Доверенности
        [PermissionCode.ViewPowersOfAttorney] = new PermissionDescription
        {
            TitleRu = "Просмотр реестра доверенностей",
            TitleEn = "View the powers of attorney registry",
            TitleKg = "Ишеним каттардын реестрин көрүү"
        },
        [PermissionCode.ManagePowersOfAttorney] = new PermissionDescription
        {
            TitleRu = "Ведение доверенностей: выдача, подписание, отзыв",
            TitleEn = "Manage powers of attorney: issue, sign, revoke",
            TitleKg = "Ишеним каттарды жүргүзүү: берүү, кол коюу, кайра чакыртып алуу"
        },

        // Корреспонденция
        [PermissionCode.ViewCorrespondence] = new PermissionDescription
        {
            TitleRu = "Просмотр книги корреспонденции",
            TitleEn = "View the correspondence book",
            TitleKg = "Кат алышуу китебин көрүү"
        },
        [PermissionCode.RegisterCorrespondence] = new PermissionDescription
        {
            TitleRu = "Регистрация входящих/исходящих писем и ведение справочника корреспондентов",
            TitleEn = "Register incoming/outgoing letters and manage the correspondents directory",
            TitleKg = "Кирүүчү/чыгуучу каттарды каттоо жана корреспонденттердин маалымдамасын жүргүзүү"
        },
        [PermissionCode.ViewBankSecrecyInquiries] = new PermissionDescription
        {
            TitleRu = "Доступ к запросам по банковской тайне",
            TitleEn = "Access to bank-secrecy inquiries",
            TitleKg = "Банктык сырга байланыштуу суроо-талаптарга кирүү"
        },

        // Кадровые приказы
        [PermissionCode.ViewHrOrders] = new PermissionDescription
        {
            TitleRu = "Просмотр книги кадровых приказов",
            TitleEn = "View the HR orders book",
            TitleKg = "Кадрдык буйруктардын китебин көрүү"
        },
        [PermissionCode.ManageHrOrders] = new PermissionDescription
        {
            TitleRu = "Издание кадровых приказов: создание, подписание, ознакомление",
            TitleEn = "Issue HR orders: create, sign, acknowledge",
            TitleKg = "Кадрдык буйруктарды чыгаруу: түзүү, кол коюу, тааныштыруу"
        },

        // Закупки
        [PermissionCode.ViewAllProcurements] = new PermissionDescription
        {
            TitleRu = "Видеть все заявки на закупку, а не только свои",
            TitleEn = "See all procurement requests, not only own",
            TitleKg = "Өздүкүн гана эмес, бардык сатып алуу арыздарын көрүү"
        },
        [PermissionCode.ConductProcurement] = new PermissionDescription
        {
            TitleRu = "Вести закупочную процедуру (Сектор закупок): конкурс, публикация, вскрытие",
            TitleEn = "Conduct the procurement procedure (Procurement Unit): tender, publication, bid opening",
            TitleKg = "Сатып алуу жол-жобосун жүргүзүү (Сатып алуу сектору)"
        },
        [PermissionCode.RecordCommissionDecisions] = new PermissionDescription
        {
            TitleRu = "Вносить решения комиссии по закупке (секретарь комиссии)",
            TitleEn = "Record procurement commission decisions (commission secretary)",
            TitleKg = "Сатып алуу комиссиясынын чечимдерин киргизүү (комиссиянын катчысы)"
        },
        [PermissionCode.ManageProcurementProtocol] = new PermissionDescription
        {
            TitleRu = "Формировать протокол закупки",
            TitleEn = "Form the procurement protocol",
            TitleKg = "Сатып алуу протоколун түзүү"
        },
        [PermissionCode.ManageProcurementContracts] = new PermissionDescription
        {
            TitleRu = "Договоры по закупкам: акты, претензии, исполнение",
            TitleEn = "Procurement contracts: acts, claims, fulfilment",
            TitleKg = "Сатып алуу боюнча келишимдер: актылар, дооматтар, аткаруу"
        },
        [PermissionCode.ManageProcurementPlan] = new PermissionDescription
        {
            TitleRu = "Вести План закупок",
            TitleEn = "Manage the procurement plan",
            TitleKg = "Сатып алуу планын жүргүзүү"
        },
        [PermissionCode.ManageSuppliers] = new PermissionDescription
        {
            TitleRu = "Реестр поставщиков и чёрный список недобросовестных",
            TitleEn = "Suppliers registry and blacklist of unreliable suppliers",
            TitleKg = "Жеткирүүчүлөрдүн реестри жана кара тизме"
        },

        // Служебные записки
        [PermissionCode.ViewAllSz] = new PermissionDescription
        {
            TitleRu = "Видеть все служебные записки банка, а не только свои и своего подразделения",
            TitleEn = "See all internal memos of the bank, not only own and own unit's",
            TitleKg = "Банктын бардык кызматтык каттарын көрүү"
        },
        [PermissionCode.RegisterSz] = new PermissionDescription
        {
            TitleRu = "Регистрировать служебные записки: присваивать номер согласованной записке",
            TitleEn = "Register internal memos: assign a number to an approved memo",
            TitleKg = "Кызматтык каттарды каттоо: макулдашылган катка номер берүү"
        },
        [PermissionCode.SubmitSzToBody] = new PermissionDescription
        {
            TitleRu = "Выносить служебные записки на коллегиальный орган",
            TitleEn = "Submit internal memos to a collegial body",
            TitleKg = "Кызматтык каттарды коллегиалдуу органга чыгаруу"
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