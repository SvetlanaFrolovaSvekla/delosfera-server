using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/// <summary>Что именно изменилось в подразделении (GEN-08).</summary>
public enum OrgUnitChangeKind
{
    Created = 1,
    Renamed = 2,

    /// <summary>Переподчинение: изменился родитель.</summary>
    Moved = 3,

    HeadChanged = 4,
    CuratorChanged = 5,

    /// <summary>Прочие реквизиты — например, признак бумажных записок.</summary>
    AttributesChanged = 6,

    Removed = 7,
}

/// <summary>
/// Запись истории изменений оргструктуры (GEN-08).
///
/// Оргструктура банка меняется приказами, и «как было на прошлый квартал» —
/// не любопытство, а обязательный вопрос при разборе согласований: маршрут
/// строился по руководителям и кураторам, действовавшим на тот момент.
///
/// История ведётся отдельной таблицей, а не восстанавливается из общего аудита:
/// в аудите лежат сырые действия пользователей, а здесь — состояние справочника
/// на каждую дату, пригодное для отчёта и для ответа проверяющему.
/// </summary>
public class OrganizationUnitHistory
{
    public int Id { get; set; }

    public int OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public OrgUnitChangeKind Kind { get; set; }

    /// <summary>Значения до и после — в человекочитаемом виде, чтобы отчёт не требовал расшифровки.</summary>
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>Основание: приказ, распоряжение. Заполняется, если указано.</summary>
    public string? Reason { get; set; }

    /// <summary>С какой даты изменение действует. Может отличаться от даты внесения в систему.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime At { get; set; }
}

public class OrganizationUnitHistoryConfiguration : IEntityTypeConfiguration<OrganizationUnitHistory>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitHistory> b)
    {
        b.ToTable("dictionary_organization_unit_history");
        b.Property(x => x.Kind).HasConversion<int>();

        // Выборка «что было с подразделением» и «что менялось на дату» — основные.
        b.HasIndex(x => new {x.OrgUnitId, x.EffectiveFrom});
        b.HasIndex(x => x.EffectiveFrom);

        b.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
