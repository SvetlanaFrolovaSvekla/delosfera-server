using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Кто в маршруте кадровых СЗ выступает кадровиком УЧР — по области автора (КСЗ-04..06).
/// Разделение «Головной офис / филиальная сеть» из ТР: УЧР по ГО и УЧР по филиалам —
/// разные люди. Одна строка на систему; ведёт УЧР без участия разработчика (КСЗ-12).
/// </summary>
public class HrRoutingSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Кадровик УЧР для записок Головного офиса.</summary>
    public int? HeadOfficeHrUserId { get; set; }

    /// <summary>Кадровик УЧР для записок филиальной сети.</summary>
    public int? BranchHrUserId { get; set; }

    /// <summary>Согласующий от Операционного управления в маршруте заявок на замещение (ЗМ-Настр).</summary>
    public int? OperationsApproverUserId { get; set; }

    /// <summary>Замена согласующего Операционного управления при его отсутствии.</summary>
    public int? OperationsFallbackUserId { get; set; }

    /// <summary>
    /// Кто подписывает приказ и договор о материальной ответственности по заявкам на замещение
    /// сотрудников Головного офиса. У филиала это директор филиала (этап маршрута), а у Головного
    /// офиса директора филиала нет — без этой настройки подпись «Директор» не формируется.
    /// </summary>
    public int? HeadOfficeSignerUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
