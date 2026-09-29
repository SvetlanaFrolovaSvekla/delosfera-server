using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Signing.Models;

/// <summary>
/// Сохранённая идентификация пользователя в облачной ЭЦП ГП «Инфоком»: ПИН (14 цифр) и код из
/// СМС/почты (byPin для 2.3 account/auth) в зашифрованном виде. Пока запись действует, сервер
/// сам обменивает код на минутный токен доступа перед каждым подписанием резолюции, и
/// пользователю не нужно заново вводить ПИН и код (см. UserEcpCredentialService).
///
/// Код хранится ТОЛЬКО зашифрованным (AES-256-GCM, ключ из конфигурации EcpCredentials:
/// EncryptionKey, см. EcpCredentialProtector) и привязан к пользователю и ПИН через AAD — строку
/// нельзя "перенести" на другую учётку. Открытым текстом код нигде не хранится и не логируется.
/// Срок действия ограничен (ExpiresAt) — по истечении запись удаляется, нужна новая идентификация.
/// Одна запись на пользователя. Пользователь может отозвать её в любой момент.
/// </summary>
public class UserEcpCredential
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>ПИН физического лица (14 цифр).</summary>
    public string PersonIdnp { get; set; } = "";

    /// <summary>ИНН организации, если подписывают от её имени (представитель юр. лица); null — физ. лицо.</summary>
    public string? OrganizationInn { get; set; }

    /// <summary>base64(nonce || tag || ciphertext) кода из СМС/почты.</summary>
    public string EncryptedCode { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    /// <summary>До какого момента наша система готова использовать сохранённый код.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}

public class UserEcpCredentialConfiguration : IEntityTypeConfiguration<UserEcpCredential>
{
    public void Configure(EntityTypeBuilder<UserEcpCredential> b)
    {
        b.ToTable("user_ecp_credential");
        b.HasKey(x => x.Id).HasName("pk_user_ecp_credential");

        b.Property(x => x.PersonIdnp).HasMaxLength(14);
        b.Property(x => x.OrganizationInn).HasMaxLength(20);
        b.Property(x => x.EncryptedCode).HasMaxLength(512);

        // Одна идентификация на пользователя.
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_user_ecp_credential_user_id");

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_ecp_credential_user_user_id");
    }
}
