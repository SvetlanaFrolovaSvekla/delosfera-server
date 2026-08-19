using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Workflow.Models.Configurations;

public class RouteInstanceConfiguration : IEntityTypeConfiguration<RouteInstance>
{
    public void Configure(EntityTypeBuilder<RouteInstance> b)
    {
        b.ToTable("route_instance");
        b.Property(x => x.Status).HasConversion<string>();
        b.HasIndex(x => x.DocumentId);
    }
}

public class RouteStepConfiguration : IEntityTypeConfiguration<RouteStep>
{
    public void Configure(EntityTypeBuilder<RouteStep> b)
    {
        b.ToTable("route_step");
        b.Property(x => x.Mode).HasConversion<string>();
        b.Property(x => x.Kind).HasConversion<string>();
        b.HasOne(x => x.RouteInstance).WithMany(i => i.Steps)
            .HasForeignKey(x => x.RouteInstanceId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.RouteInstanceId, x.Order });
    }
}

public class RouteParticipantConfiguration : IEntityTypeConfiguration<RouteParticipant>
{
    public void Configure(EntityTypeBuilder<RouteParticipant> b)
    {
        b.ToTable("route_participant");
        b.Property(x => x.State).HasConversion<string>();
        b.HasOne(x => x.RouteStep).WithMany(s => s.Participants)
            .HasForeignKey(x => x.RouteStepId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.UserId);
    }
}

public class ResolutionConfiguration : IEntityTypeConfiguration<Resolution>
{
    public void Configure(EntityTypeBuilder<Resolution> b)
    {
        b.ToTable("resolution");
        b.Property(x => x.Type).HasConversion<string>();
        b.HasOne(x => x.RouteParticipant).WithOne(p => p.Resolution)
            .HasForeignKey<Resolution>(x => x.RouteParticipantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.RouteParticipantId).IsUnique();
    }
}

public class RemarkConfiguration : IEntityTypeConfiguration<Remark>
{
    public void Configure(EntityTypeBuilder<Remark> b)
    {
        b.ToTable("remark");
        b.Property(x => x.State).HasConversion<string>();
        b.HasOne(x => x.Resolution).WithMany(r => r.Remarks)
            .HasForeignKey(x => x.ResolutionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> b)
    {
        b.ToTable("workflow_task");
        b.Property(x => x.State).HasConversion<string>();
        b.HasIndex(x => new { x.AssigneeUserId, x.State });
    }
}

public class RouteTemplateConfiguration : IEntityTypeConfiguration<RouteTemplate>
{
    public void Configure(EntityTypeBuilder<RouteTemplate> b)
    {
        b.ToTable("route_template");
        b.Property(x => x.DocumentType).HasConversion<string>();
    }
}

public class RouteTemplateStepConfiguration : IEntityTypeConfiguration<RouteTemplateStep>
{
    public void Configure(EntityTypeBuilder<RouteTemplateStep> b)
    {
        b.ToTable("route_template_step");
        b.Property(x => x.Mode).HasConversion<string>();
        b.Property(x => x.Kind).HasConversion<string>();
        b.HasOne(x => x.RouteTemplate).WithMany(t => t.Steps)
            .HasForeignKey(x => x.RouteTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RouteTemplateParticipantConfiguration : IEntityTypeConfiguration<RouteTemplateParticipant>
{
    public void Configure(EntityTypeBuilder<RouteTemplateParticipant> b)
    {
        b.ToTable("route_template_participant");
        b.HasOne(x => x.RouteTemplateStep).WithMany(s => s.Participants)
            .HasForeignKey(x => x.RouteTemplateStepId).OnDelete(DeleteBehavior.Cascade);
    }
}
