using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Meetings.Models.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> b)
    {
        b.ToTable("meeting");
        b.Property(x => x.Body).HasConversion<int>();
        b.Property(x => x.Form).HasConversion<int>();

        // Счётчик обнуляется 1 января и ведётся отдельно по органу: номер «01» в один
        // и тот же год может существовать и у Правления, и у КПА, но не дважды у одного.
        b.HasIndex(x => new { x.Year, x.Body, x.Number }).IsUnique();

        b.HasOne(x => x.Secretary)
            .WithMany()
            .HasForeignKey(x => x.SecretaryUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SecretaryUnit)
            .WithMany()
            .HasForeignKey(x => x.SecretaryUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Items)
            .WithOne(i => i.Meeting!)
            .HasForeignKey(i => i.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgendaItemConfiguration : IEntityTypeConfiguration<AgendaItem>
{
    public void Configure(EntityTypeBuilder<AgendaItem> b)
    {
        b.ToTable("meeting_agenda_item");

        // Поисковый вектор по теме, решению и номеру протокола — вычисляется базой (GEN-04).
        b.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(topic, '') || ' ' || coalesce(decision, '') || ' ' || coalesce(protocol_number, ''))",
                stored: true);

        b.HasIndex(x => x.SearchVector).HasMethod("GIN");

        b.HasIndex(x => new { x.MeetingId, x.Order });

        b.HasOne(x => x.Speaker).WithMany()
            .HasForeignKey(x => x.SpeakerUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SpeakerHead).WithMany()
            .HasForeignKey(x => x.SpeakerHeadUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.DeputySecretary).WithMany()
            .HasForeignKey(x => x.DeputySecretaryUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Controller).WithMany()
            .HasForeignKey(x => x.ControllerUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SpeakerUnit)
            .WithMany()
            .HasForeignKey(x => x.SpeakerUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Guests)
            .WithOne(g => g.AgendaItem!)
            .HasForeignKey(g => g.AgendaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Assignments)
            .WithOne(a => a.AgendaItem!)
            .HasForeignKey(a => a.AgendaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Files)
            .WithOne(f => f.AgendaItem!)
            .HasForeignKey(f => f.AgendaItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgendaGuestConfiguration : IEntityTypeConfiguration<AgendaGuest>
{
    public void Configure(EntityTypeBuilder<AgendaGuest> b)
    {
        b.ToTable("meeting_agenda_guest");

        // Одного человека не приглашают на вопрос дважды.
        b.HasIndex(x => new { x.AgendaItemId, x.UserId }).IsUnique();

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgendaAssignmentConfiguration : IEntityTypeConfiguration<AgendaAssignment>
{
    public void Configure(EntityTypeBuilder<AgendaAssignment> b)
    {
        b.ToTable("meeting_agenda_assignment");
        b.Property(x => x.Status).HasConversion<int>();

        // Фоновая рассылка выбирает поручения по сроку и статусу — индекс под неё.
        b.HasIndex(x => new { x.DueDate, x.Status });

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgendaFileConfiguration : IEntityTypeConfiguration<AgendaFile>
{
    public void Configure(EntityTypeBuilder<AgendaFile> b)
    {
        b.ToTable("meeting_agenda_file");
        b.Property(x => x.Kind).HasConversion<int>();

        b.HasIndex(x => new { x.AgendaItemId, x.Kind });

        b.HasOne(x => x.File)
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
