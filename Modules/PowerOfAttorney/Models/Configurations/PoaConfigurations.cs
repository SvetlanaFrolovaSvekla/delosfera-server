using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.PowerOfAttorney.Models.Configurations;

public class PowerOfAttorneyConfiguration : IEntityTypeConfiguration<PowerOfAttorney>
{
    public void Configure(EntityTypeBuilder<PowerOfAttorney> builder)
    {
        builder.ToTable("power_of_attorney");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegNumber).HasMaxLength(50);
        builder.Property(x => x.HolderName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HolderPosition).HasMaxLength(300);
        builder.Property(x => x.HolderIdentityDocument).HasMaxLength(500);
        builder.Property(x => x.Powers).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.AmountCurrency).HasMaxLength(3);
        builder.Property(x => x.RevokeReason).HasMaxLength(1000);
        builder.Property(x => x.OriginalLocation).HasMaxLength(300);

        // Деньги в decimal, а не double: предельная сумма сделки сравнивается на
        // равенство, и округление двоичной дроби здесь недопустимо.
        builder.Property(x => x.AmountLimit).HasPrecision(18, 2);

        builder.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(holder_name, '') || ' ' || coalesce(powers, '') || ' ' || coalesce(reg_number, ''))",
                stored: true);

        builder.HasIndex(x => x.SearchVector).HasMethod("GIN");

        builder.HasOne(x => x.GrantorUser)
            .WithMany()
            .HasForeignKey(x => x.GrantorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HolderUser)
            .WithMany()
            .HasForeignKey(x => x.HolderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HolderUnit)
            .WithMany()
            .HasForeignKey(x => x.HolderUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Передоверие: доверенность ссылается на ту, по которой выдана. Удаление
        // родительской не должно уносить дочерние — их надо разбирать руками.
        builder.HasOne(x => x.ParentPoa)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentPoaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Номер уникален в пределах года — как в книге регистрации на бумаге.
        builder.HasIndex(x => new { x.Year, x.RegNumber })
            .IsUnique()
            .HasFilter("reg_number IS NOT NULL");

        // «Что действует у этого человека» и «что истекает на неделе» — два вопроса,
        // ради которых реестр и заводят.
        builder.HasIndex(x => new { x.HolderUserId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ValidTo });
    }
}

public class PoaFileConfiguration : IEntityTypeConfiguration<PoaFile>
{
    public void Configure(EntityTypeBuilder<PoaFile> builder)
    {
        builder.ToTable("poa_file");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.PowerOfAttorney)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.PowerOfAttorneyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.File)
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
