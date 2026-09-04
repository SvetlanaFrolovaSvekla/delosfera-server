using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Hr.Models.Configurations;

public class HrOrderConfiguration : IEntityTypeConfiguration<HrOrder>
{
    public void Configure(EntityTypeBuilder<HrOrder> builder)
    {
        builder.ToTable("hr_order");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegNumber).HasMaxLength(50);
        builder.Property(x => x.Title).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(20000);
        builder.Property(x => x.Basis).HasMaxLength(1000);

        // Поисковый образ считает база: заголовок, текст, основание и номер.
        builder.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(title, '') || ' ' || coalesce(body, '') || ' ' " +
                "|| coalesce(basis, '') || ' ' || coalesce(reg_number, ''))",
                stored: true);

        builder.HasIndex(x => x.SearchVector).HasMethod("GIN");

        builder.HasOne(x => x.SignerUser)
            .WithMany()
            .HasForeignKey(x => x.SignerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.NomenclatureCase)
            .WithMany()
            .HasForeignKey(x => x.NomenclatureCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Отменяющий приказ ссылается на отменяемый. Удаление отменяемого не должно
        // уносить отменяющий: он подписан и существует независимо.
        builder.HasOne(x => x.CancelsOrder)
            .WithMany()
            .HasForeignKey(x => x.CancelsOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Книга приказов по личному составу: номер уникален в пределах года.
        builder.HasIndex(x => new { x.Year, x.RegNumber })
            .IsUnique()
            .HasFilter("reg_number IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.OrderDate });
        builder.HasIndex(x => x.Kind);
    }
}

public class HrOrderEmployeeConfiguration : IEntityTypeConfiguration<HrOrderEmployee>
{
    public void Configure(EntityTypeBuilder<HrOrderEmployee> builder)
    {
        builder.ToTable("hr_order_employee");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.PositionSnapshot).HasMaxLength(300);
        builder.Property(x => x.UnitSnapshot).HasMaxLength(300);
        builder.Property(x => x.FieldValues).HasColumnType("jsonb");

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // «Что было по этому сотруднику» — главный вопрос к книге приказов.
        builder.HasIndex(x => x.UserId);

        // Один сотрудник в приказе один раз: дважды указанный получил бы два
        // приказа об одном и том же.
        builder.HasIndex(x => new { x.OrderId, x.UserId }).IsUnique();
    }
}

/// <summary>Сканы подписанного приказа.</summary>
public class HrOrderFileConfiguration : IEntityTypeConfiguration<HrOrderFile>
{
    public void Configure(EntityTypeBuilder<HrOrderFile> builder)
    {
        builder.ToTable("hr_order_file");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Файл удалять вместе с привязкой нельзя: он живёт в общем хранилище и
        // может быть приложен не только к приказу.
        builder.HasOne(x => x.File)
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderId);

        // Один и тот же файл дважды к приказу не прикладывается.
        builder.HasIndex(x => new { x.OrderId, x.FileId }).IsUnique();
    }
}

