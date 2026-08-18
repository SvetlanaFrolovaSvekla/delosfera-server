using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Sz.Models.Configurations;

public class SzKindConfiguration : IEntityTypeConfiguration<SzKind>
{
    public void Configure(EntityTypeBuilder<SzKind> b)
    {
        b.ToTable("sz_kind");
        b.Property(x => x.FormKey).HasConversion<string>();

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Виды из ТЗ по СЗ; администратор добавляет свои, выбирая одну из форм.
        b.HasData(
            new { Id = 1, TitleRu = "Кадровая", TitleEn = "HR", TitleKg = "Кадрдык", FormKey = SzFormKey.Hr, IsPaperByDefault = true, ExecutionDays = 14, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "На закупку", TitleEn = "Procurement", TitleKg = "Сатып алууга", FormKey = SzFormKey.Procurement, IsPaperByDefault = false, ExecutionDays = 14, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "На обучение", TitleEn = "Training", TitleKg = "Окутууга", FormKey = SzFormKey.Training, IsPaperByDefault = false, ExecutionDays = 14, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Прочие", TitleEn = "Other", TitleKg = "Башка", FormKey = SzFormKey.Other, IsPaperByDefault = false, ExecutionDays = 14, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}

public class SzHrKindConfiguration : IEntityTypeConfiguration<SzHrKind>
{
    public void Configure(EntityTypeBuilder<SzHrKind> b)
    {
        b.ToTable("sz_hr_kind");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Перечень видов кадровых СЗ задан ТЗ (Jira-паритет).
        b.HasData(
            new { Id = 1, TitleRu = "Изменение оклада", TitleEn = "Salary change", TitleKg = "Эмгек акыны өзгөртүү", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Командировка", TitleEn = "Business trip", TitleKg = "Иш сапар", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "Перемещение с изменением оклада", TitleEn = "Transfer with salary change", TitleKg = "Эмгек акы өзгөрүү менен которуу", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Перемещение без изменения оклада", TitleEn = "Transfer without salary change", TitleKg = "Эмгек акы өзгөрбөй которуу", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Приём на работу", TitleEn = "Hiring", TitleKg = "Жумушка кабыл алуу", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Приём стажёров с оплатой", TitleEn = "Paid internship", TitleKg = "Акы төлөнүүчү стажировка", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Приём стажёров без оплаты", TitleEn = "Unpaid internship", TitleKg = "Акысыз стажировка", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 8, TitleRu = "Работа в выходной день", TitleEn = "Work on day off", TitleKg = "Эс алуу күнү иштөө", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 9, TitleRu = "Другое", TitleEn = "Other", TitleKg = "Башка", IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}

public class SzDocumentConfiguration : IEntityTypeConfiguration<SzDocument>
{
    public void Configure(EntityTypeBuilder<SzDocument> b)
    {
        b.ToTable("sz_document");

        // Поисковый вектор по тексту записки и резолюции — вычисляется базой (GEN-04).
        b.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(body, '') || ' ' || coalesce(execution_resolution, ''))",
                stored: true);

        b.HasIndex(x => x.SearchVector).HasMethod("GIN");

        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.ExtraFields).HasColumnType("jsonb");

        // Единая карточка документа удаляется вместе с записью контура.
        b.HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.DocumentId).IsUnique();

        b.HasOne(x => x.Kind).WithMany()
            .HasForeignKey(x => x.KindId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.HrKind).WithMany()
            .HasForeignKey(x => x.HrKindId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.AuthorUnit).WithMany()
            .HasForeignKey(x => x.AuthorUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CorrespondentUnit).WithMany()
            .HasForeignKey(x => x.CorrespondentUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EmployeeUnit).WithMany()
            .HasForeignKey(x => x.EmployeeUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TransferUnit).WithMany()
            .HasForeignKey(x => x.TransferUnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SignerUser).WithMany()
            .HasForeignKey(x => x.SignerUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OriginalHolderUser).WithMany()
            .HasForeignKey(x => x.OriginalHolderUserId).OnDelete(DeleteBehavior.Restrict);

        // Реестр невозвращённых оригиналов ходит по держателю и сроку возврата.
        b.HasIndex(x => new { x.OriginalHolderUserId, x.OriginalReturnedAt });

        // Рубрикатор — мультипапка: записка лежит в нескольких рубриках сразу.
        b.HasMany(x => x.Rubrics).WithMany()
            .UsingEntity(j => j.ToTable("sz_document_rubric"));

        b.HasIndex(x => x.KindId);
        b.HasIndex(x => x.DueDate);
    }
}

public class SzAssignmentConfiguration : IEntityTypeConfiguration<SzAssignment>
{
    public void Configure(EntityTypeBuilder<SzAssignment> b)
    {
        b.ToTable("sz_assignment");

        b.Property(x => x.State).HasConversion<string>();
        b.Property(x => x.Text).IsRequired();

        // Поручения живут вместе с запиской.
        b.HasOne(x => x.SzDocument).WithMany(x => x.Assignments)
            .HasForeignKey(x => x.SzDocumentId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AssigneeUser).WithMany()
            .HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AssigneeUnit).WithMany()
            .HasForeignKey(x => x.AssigneeUnitId).OnDelete(DeleteBehavior.Restrict);

        // Очередь «Мои поручения» и контроль просрочки идут по этим полям.
        b.HasIndex(x => new { x.AssigneeUserId, x.State });
        b.HasIndex(x => x.DueDate);
    }
}

public class SzEmployeeConfiguration : IEntityTypeConfiguration<SzEmployee>
{
    public void Configure(EntityTypeBuilder<SzEmployee> b)
    {
        b.ToTable("sz_employee");
        b.Property(x => x.FullName).HasMaxLength(300);
        b.Property(x => x.Position).HasMaxLength(300);

        // Список сотрудников всегда читается вместе с запиской и в заданном порядке.
        b.HasIndex(x => new {x.SzDocumentId, x.SortOrder});

        b.HasOne(x => x.SzDocument)
            .WithMany(d => d.Employees)
            .HasForeignKey(x => x.SzDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
