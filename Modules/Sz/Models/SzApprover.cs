using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Согласующий по служебной записке.
///
/// Список ведётся на самой записке: маршрут по шаблону вида подходит типовым
/// запискам, но большинство согласуется адресно — автор выбирает тех, чьё мнение
/// нужно именно по этому вопросу.
/// </summary>
public class SzApprover
{
    public int Id { get; set; }

    public int SzDocumentId { get; set; }
    public SzDocument? SzDocument { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Очерёдность при последовательном согласовании; при параллельном не используется.</summary>
    public int Order { get; set; }
}

public class SzApproverConfiguration : IEntityTypeConfiguration<SzApprover>
{
    public void Configure(EntityTypeBuilder<SzApprover> b)
    {
        b.ToTable("sz_approver");

        // Один и тот же сотрудник не согласует записку дважды.
        b.HasIndex(x => new {x.SzDocumentId, x.UserId}).IsUnique();

        b.HasOne(x => x.SzDocument)
            .WithMany(x => x.Approvers)
            .HasForeignKey(x => x.SzDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
