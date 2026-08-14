using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Signing.Models;

/// <summary>
/// Регламент применения простой электронной подписи.
///
/// Закон КР «Об электронной подписи» признаёт простую подпись равнозначной
/// собственноручной, когда стороны договорились о правилах её применения. В
/// системе этой договорённостью служит согласие сотрудника с регламентом: без
/// него нажатие кнопки «Согласовать» остаётся действием в программе, а не
/// подписью, которую можно предъявить.
///
/// Текст хранится записью, а не константой: правила уточняет юридическая служба,
/// и правка не должна требовать пересборки. У текста есть версия — при её смене
/// согласие спрашивается заново, потому что человек соглашался с прежними
/// условиями, а не с новыми.
/// </summary>
public class SimpleSignatureRegulation
{
    public int Id { get; set; }

    /// <summary>Версия редакции: при смене согласие спрашивается заново.</summary>
    public required string Version { get; set; }

    public required string Title { get; set; }

    /// <summary>Текст регламента, который читает сотрудник перед согласием.</summary>
    public required string Body { get; set; }

    /// <summary>Регламент действует; выключенный не показывается и согласия не требует.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Согласие сотрудника с регламентом. Хранится отдельной записью, а не флагом в
/// карточке: важно, с какой именно редакцией человек согласился и когда.
/// </summary>
public class SimpleSignatureConsent
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Версия регламента на момент согласия.</summary>
    public required string Version { get; set; }

    public DateTime AcceptedAt { get; set; }
}

public class SimpleSignatureRegulationConfiguration : IEntityTypeConfiguration<SimpleSignatureRegulation>
{
    public void Configure(EntityTypeBuilder<SimpleSignatureRegulation> b)
    {
        b.ToTable("simple_signature_regulation");
        b.Property(x => x.Version).HasMaxLength(32);
        b.Property(x => x.Title).HasMaxLength(300);
    }
}

public class SimpleSignatureConsentConfiguration : IEntityTypeConfiguration<SimpleSignatureConsent>
{
    public void Configure(EntityTypeBuilder<SimpleSignatureConsent> b)
    {
        b.ToTable("simple_signature_consent");
        b.Property(x => x.Version).HasMaxLength(32);

        // Одно согласие на человека и редакцию: повторное нажатие не должно
        // плодить записи, а история по версиям обязана сохраниться.
        b.HasIndex(x => new {x.UserId, x.Version}).IsUnique();
    }
}
