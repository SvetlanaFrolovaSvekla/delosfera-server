using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndProposalConfiguration : IEntityTypeConfiguration<VndProposal>
{
    public void Configure(EntityTypeBuilder<VndProposal> builder)
    {
        builder.ToTable("vnd_proposal");

        builder.Property(x => x.Text).IsRequired().HasMaxLength(VndProposalLimits.MaxTextLength);

        builder.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Redaction)
            .WithMany()
            .HasForeignKey(x => x.RedactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AuthorUser)
            .WithMany()
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReadByUser)
            .WithMany()
            .HasForeignKey(x => x.ReadByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ReadAt);
    }
}

public class VndProposalQuoteConfiguration : IEntityTypeConfiguration<VndProposalQuote>
{
    public void Configure(EntityTypeBuilder<VndProposalQuote> builder)
    {
        builder.ToTable("vnd_proposal_quote");

        builder.Property(x => x.DocumentTarget).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Text).IsRequired();

        builder.HasOne(x => x.Proposal)
            .WithMany(x => x.Quotes)
            .HasForeignKey(x => x.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VndProposalAttachmentConfiguration : IEntityTypeConfiguration<VndProposalAttachment>
{
    public void Configure(EntityTypeBuilder<VndProposalAttachment> builder)
    {
        builder.ToTable("vnd_proposal_attachment");

        builder.HasOne(x => x.Proposal)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
