using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Стадия годового плана актуализации (PLN-01).</summary>
public enum ActualizationPlanStatus
{
    /// <summary>Формируется: позиции добавляются, импортируются и правятся.</summary>
    Draft = 1,

    /// <summary>Утверждён — позиции меняются только с указанием причины.</summary>
    Approved = 2,

    /// <summary>Год закрыт.</summary>
    Closed = 3,
}

/// <summary>Состояние позиции плана (PLN-02, PLN-06).</summary>
public enum PlanItemStatus
{
    /// <summary>Запланировано: срок актуализации ещё не наступил, работа не начата.</summary>
    Planned = 1,

    /// <summary>На актуализации: по ВНД идёт процесс изменений.</summary>
    OnActualization = 2,

    /// <summary>Актуально: изменения утверждены, назначен новый срок.</summary>
    Actual = 3,

    /// <summary>Снято с плана — с указанием причины.</summary>
    Excluded = 4,
}

/// <summary>
/// Годовой план актуализации ВНД (PLN-01).
///
/// План ведётся по годам и наполняется импортом из Excel: Отдел методологии
/// формирует его в таблице задолго до того, как позиции появляются в системе.
/// </summary>
public class ActualizationPlan : IAuditableEntity
{
    public int Id { get; set; }

    public int Year { get; set; }

    public ActualizationPlanStatus Status { get; set; } = ActualizationPlanStatus.Draft;

    /// <summary>Чем утверждён план — протокол или распоряжение.</summary>
    public string? ApprovalNote { get; set; }
    public DateOnly? ApprovedOn { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ActualizationPlanItem> Items { get; set; } = new List<ActualizationPlanItem>();
}

/// <summary>
/// Позиция плана: какой документ и к какому сроку должен быть актуализирован.
///
/// Ссылка на ВНД необязательна: в импортируемой таблице документ назван словами, и
/// часть строк не сопоставляется автоматически — плановую позицию всё равно надо
/// видеть, иначе она молча выпадет из контроля.
/// </summary>
public class ActualizationPlanItem : IAuditableEntity
{
    public int Id { get; set; }

    public int PlanId { get; set; }
    public ActualizationPlan? Plan { get; set; }

    /// <summary>Порядковый номер позиции из таблицы плана.</summary>
    public int Order { get; set; }

    /// <summary>Наименование ВНД так, как оно записано в плане.</summary>
    public required string Title { get; set; }

    /// <summary>Сопоставленный документ базы ВНД; null — соответствие не найдено.</summary>
    public int? VndDocumentId { get; set; }
    public VndDocument? VndDocument { get; set; }

    /// <summary>Ответственное подразделение — владелец позиции.</summary>
    public int? ResponsibleUnitId { get; set; }
    public OrganizationUnit? ResponsibleUnit { get; set; }

    /// <summary>
    /// Курирующий заместитель Председателя Правления: получает копии критических
    /// напоминаний и уведомлений о просрочке (PLN-04).
    /// </summary>
    public int? CuratorUserId { get; set; }
    public User? Curator { get; set; }

    public int? ApprovalBodyId { get; set; }
    public ApprovalBody? ApprovalBody { get; set; }

    /// <summary>Плановая дата актуализации.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Дата следующей актуализации — проставляется после утверждения изменений.</summary>
    public DateOnly? NextDueDate { get; set; }

    public PlanItemStatus Status { get; set; } = PlanItemStatus.Planned;

    public string? Comment { get; set; }

    /// <summary>Когда по позиции запущена актуализация — от неё считается фактический срок.</summary>
    public DateOnly? StartedOn { get; set; }

    /// <summary>Когда изменения утверждены.</summary>
    public DateOnly? CompletedOn { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PlanItemEvent> Events { get; set; } = new List<PlanItemEvent>();
}

/// <summary>
/// Запись журнала операций по позиции плана (PLN-07).
///
/// Журнал ведётся отдельно от общего аудита системы: Отделу методологии нужна
/// история конкретной позиции — кто перенёс срок, когда началась актуализация,
/// почему позицию сняли, — а не выборка из общего потока событий.
/// </summary>
public class PlanItemEvent
{
    public int Id { get; set; }

    public int PlanItemId { get; set; }
    public ActualizationPlanItem? PlanItem { get; set; }

    public required string Kind { get; set; }
    public required string Description { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public DateTime At { get; set; }
}

public class ActualizationPlanConfiguration : IEntityTypeConfiguration<ActualizationPlan>
{
    public void Configure(EntityTypeBuilder<ActualizationPlan> b)
    {
        b.ToTable("vnd_actualization_plan");
        b.Property(x => x.Status).HasConversion<int>();

        // План один на год: иначе позиции разъедутся по двум версиям одного года.
        b.HasIndex(x => x.Year).IsUnique();

        b.HasMany(x => x.Items)
            .WithOne(i => i.Plan!)
            .HasForeignKey(i => i.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ActualizationPlanItemConfiguration : IEntityTypeConfiguration<ActualizationPlanItem>
{
    public void Configure(EntityTypeBuilder<ActualizationPlanItem> b)
    {
        b.ToTable("vnd_actualization_plan_item");
        b.Property(x => x.Status).HasConversion<int>();

        // Дашборд и напоминания выбирают позиции по сроку и состоянию.
        b.HasIndex(x => new {x.DueDate, x.Status});

        b.HasOne(x => x.VndDocument).WithMany()
            .HasForeignKey(x => x.VndDocumentId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.ResponsibleUnit).WithMany()
            .HasForeignKey(x => x.ResponsibleUnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Curator).WithMany()
            .HasForeignKey(x => x.CuratorUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ApprovalBody).WithMany()
            .HasForeignKey(x => x.ApprovalBodyId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Events)
            .WithOne(e => e.PlanItem!)
            .HasForeignKey(e => e.PlanItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanItemEventConfiguration : IEntityTypeConfiguration<PlanItemEvent>
{
    public void Configure(EntityTypeBuilder<PlanItemEvent> b)
    {
        b.ToTable("vnd_actualization_plan_item_event");
        b.HasIndex(x => new {x.PlanItemId, x.At});

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
