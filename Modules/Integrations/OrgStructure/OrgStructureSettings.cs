using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Integrations.OrgStructure;

/// <summary>
/// Связь с порталом, который ведёт оргструктуру банка.
///
/// Оргструктуру ведут в портале, а не здесь: единственный источник правды должен
/// быть один. Сюда она приходит копией — чтобы согласование знало руководителя,
/// а рассылка «всем сотрудникам управления» знала состав.
///
/// Настройки лежат в базе, как и у службы каталогов: адрес и токен меняет
/// администратор системы, и такая правка не должна требовать доступа к серверу.
/// Токен хранится зашифрованным и наружу не отдаётся — в интерфейсе видно лишь,
/// задан он или нет.
///
/// Запись одна на всю систему: портал у банка один.
/// </summary>
public class OrgStructureSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Пока не включено, синхронизация не идёт ни по расписанию, ни вручную.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Адрес портала вместе с версией API, например
    /// <c>https://hub.keremetbank.kg/api/v1</c>.
    /// </summary>
    public string PortalUrl { get; set; } = string.Empty;

    /// <summary>Токен доступа в зашифрованном виде. Выдаётся администратором портала.</summary>
    public string TokenEncrypted { get; set; } = string.Empty;

    /// <summary>
    /// Как часто забирать структуру. Портал просит не опрашивать его в цикле:
    /// справочник запрашивается страницами, и одного прохода в сутки достаточно.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 1440;

    /// <summary>
    /// Заводить ли подразделения, которых нет в справочнике. Выключено —
    /// синхронизация только обновляет уже заведённые и сообщает о недостающих.
    /// Первый проход в банке обычно делают со включённым.
    /// </summary>
    public bool CreateMissingUnits { get; set; } = true;

    /// <summary>
    /// Сопоставлять сотрудников по почте, а не по логину. Портал предупреждает:
    /// при смене фамилии логин в домене иногда меняют, а почта остаётся той же
    /// и в домене, и в кадровых списках, и в портале.
    /// </summary>
    public bool MatchByEmail { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrgStructureSettingsConfiguration : IEntityTypeConfiguration<OrgStructureSettings>
{
    public void Configure(EntityTypeBuilder<OrgStructureSettings> b)
    {
        b.ToTable("org_structure_settings");

        b.Property(x => x.PortalUrl).HasMaxLength(512);
        b.Property(x => x.TokenEncrypted).HasMaxLength(2048);
    }
}
