using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Ответственный сотрудник СП за уведомления по актуализации ВНД — раздел "Уведомления" →
/// "Настройки рассылок" → "Нормотворчество" → "Ответственные сотрудники за актуализацию".
///
/// Список общий на все виды уведомлений этого раздела (сейчас — только ежемесячная сводка,
/// см. ActualizationNotificationSettings.MonthlyDigestEnabled): справочник отвечает на вопрос
/// "кто отвечает за актуализацию по этому СП", а не "кто получает конкретно это письмо" — если
/// появится второй повод для рассылки, он использует тот же список, а не заводит свой.
/// </summary>
public class ActualizationNotificationResponsible
{
    public int Id { get; set; }

    public int OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; }
}
