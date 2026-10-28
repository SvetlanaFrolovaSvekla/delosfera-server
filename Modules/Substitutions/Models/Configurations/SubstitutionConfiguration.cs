using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Substitutions.Models.Configurations;

public class SubstitutionRequestConfiguration : IEntityTypeConfiguration<SubstitutionRequest>
{
    public void Configure(EntityTypeBuilder<SubstitutionRequest> b)
    {
        b.HasOne(x => x.InitiatorUser).WithMany()
            .HasForeignKey(x => x.InitiatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AuthorUser).WithMany()
            .HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AbsentUser).WithMany()
            .HasForeignKey(x => x.AbsentUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubstituteUser).WithMany()
            .HasForeignKey(x => x.SubstituteUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CommissionChairUser).WithMany()
            .HasForeignKey(x => x.CommissionChairUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AbsentUnit).WithMany()
            .HasForeignKey(x => x.AbsentUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubstituteUnit).WithMany()
            .HasForeignKey(x => x.SubstituteUnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.CommissionMembers).WithOne(m => m.Request!)
            .HasForeignKey(m => m.RequestId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Status);
        b.Property(x => x.ExtraBankSignerUserIds).HasColumnType("integer[]");
    }
}

public class SubstitutionCommissionMemberConfiguration : IEntityTypeConfiguration<SubstitutionCommissionMember>
{
    public void Configure(EntityTypeBuilder<SubstitutionCommissionMember> b)
    {
        b.HasOne(m => m.User).WithMany()
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SubstitutionDocumentConfiguration : IEntityTypeConfiguration<SubstitutionDocument>
{
    public void Configure(EntityTypeBuilder<SubstitutionDocument> b)
    {
        b.ToTable("substitution_documents");
        b.HasOne(x => x.Request).WithMany()
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.FileName).HasMaxLength(300);
        b.Property(x => x.ContentSha256).HasMaxLength(64);
        b.Property(x => x.ExtraBankSignerUserIds).HasColumnType("integer[]");
        b.HasIndex(x => new { x.RequestId, x.Kind, x.Version }).IsUnique();
    }
}

public class SubstitutionSignatureConfiguration : IEntityTypeConfiguration<SubstitutionSignature>
{
    public void Configure(EntityTypeBuilder<SubstitutionSignature> b)
    {
        b.ToTable("substitution_signatures");
        // Журнал неизменяем — все связи Restrict: ни заявку, ни документ не удалить из-под подписи.
        b.HasOne(x => x.Request).WithMany()
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.PersonIdnp).HasMaxLength(14);
        b.Property(x => x.PersonFio).HasMaxLength(300);
        b.Property(x => x.OrganizationInn).HasMaxLength(20);
        b.Property(x => x.OrganizationName).HasMaxLength(500);
        b.Property(x => x.CertKeyIdentifier).HasMaxLength(200);
        b.Property(x => x.CertCommonName).HasMaxLength(300);
        b.Property(x => x.CertValidNotBefore).HasMaxLength(64);
        b.Property(x => x.CertValidNotAfter).HasMaxLength(64);
        b.Property(x => x.CertIssuerCommonName).HasMaxLength(300);
        b.Property(x => x.CertIssuerOrganizationName).HasMaxLength(500);
        b.Property(x => x.HashSchemaVersion).HasMaxLength(16);
        b.Property(x => x.Hash).HasMaxLength(64);
        b.Property(x => x.SignatureSha256).HasMaxLength(64);
        b.Property(x => x.VerificationError).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.HasIndex(x => x.SignatureSha256).IsUnique();
        b.HasIndex(x => new { x.RequestId, x.DocumentId });
        b.HasIndex(x => new { x.UserId, x.PersonIdnp });
    }
}
