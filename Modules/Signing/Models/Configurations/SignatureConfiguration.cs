using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Signing.Models.Configurations;

public class SignatureConfiguration : IEntityTypeConfiguration<Signature>
{
    public void Configure(EntityTypeBuilder<Signature> b)
    {
        b.ToTable("signature");
        b.Property(x => x.Level).HasConversion<string>();
        b.HasIndex(x => x.DocumentAttachmentId);
        b.HasIndex(x => x.DocumentId);
    }
}

public class TrustedCertificateAuthorityConfiguration : IEntityTypeConfiguration<TrustedCertificateAuthority>
{
    public void Configure(EntityTypeBuilder<TrustedCertificateAuthority> b)
    {
        b.ToTable("trusted_certificate_authority");

        // Один и тот же сертификат нельзя завести дважды: иначе снятие доверия с
        // одной записи оставляло бы вторую действующей, и центр остался бы доверенным.
        b.HasIndex(x => x.Thumbprint).IsUnique();
        b.Property(x => x.Thumbprint).HasMaxLength(128);
        b.Property(x => x.SerialNumber).HasMaxLength(128);
        b.Property(x => x.Title).HasMaxLength(300);
    }
}

public class UserCertificateConfiguration : IEntityTypeConfiguration<UserCertificate>
{
    public void Configure(EntityTypeBuilder<UserCertificate> b)
    {
        b.ToTable("user_certificate");

        // Отпечаток уникален глобально: именно этим запретом сертификат нельзя
        // закрепить сразу за двумя людьми и подписать чужой визой.
        b.HasIndex(x => x.Thumbprint).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Property(x => x.Thumbprint).HasMaxLength(128);
        b.Property(x => x.SerialNumber).HasMaxLength(128);
    }
}

public class SigningSettingsConfiguration : IEntityTypeConfiguration<SigningSettings>
{
    public void Configure(EntityTypeBuilder<SigningSettings> b)
    {
        b.ToTable("signing_settings");
        b.Property(x => x.TimestampAuthorityUrl).HasMaxLength(500);
    }
}
