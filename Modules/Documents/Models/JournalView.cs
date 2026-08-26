using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Представление журнала: какой набор колонок показывать в списке документов.
///
/// Требование банка — настраивать наборы полей без программирования. Делопроизводителю
/// в реестре записок нужны номер, дата регистрации и бумажный оригинал; руководителю —
/// автор, срок и состояние. Один набор на всех означает, что каждый второй столбец
/// лишний, а нужного нет.
///
/// Представление не привязано к виду документа жёстко: ключ журнала — строка,
/// и новый реестр подключается, не трогая базу.
/// </summary>
public class JournalView : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Какой журнал настраиваем: «sz», «vnd», «users». Строкой, а не перечислением:
    /// новый реестр не должен требовать миграции.
    /// </summary>
    public required string Journal { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Ключи колонок по порядку показа, как их называет экран. Порядок значим:
    /// он и есть порядок столбцов.
    /// </summary>
    public required string ColumnsJson { get; set; }

    /// <summary>
    /// Чьё представление. Пусто — общее, заведено администратором для всех.
    /// </summary>
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    /// <summary>
    /// Для какого подразделения. Пусто — для всех, кто видит это представление.
    ///
    /// Общее представление с подразделением показывается только его сотрудникам:
    /// набор колонок канцелярии кредитному отделу ни о чём не говорит.
    /// </summary>
    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    /// <summary>
    /// Предлагать по умолчанию при открытии журнала. Своё представление
    /// по умолчанию перевешивает общее: человек настроил под себя.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class JournalViewConfiguration : IEntityTypeConfiguration<JournalView>
{
    public void Configure(EntityTypeBuilder<JournalView> b)
    {
        b.ToTable("journal_view");

        b.Property(x => x.Journal).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.ColumnsJson).HasColumnType("jsonb");

        // Читают всегда по журналу и по владельцу: «мои представления этого
        // реестра плюс общие».
        b.HasIndex(x => new {x.Journal, x.OwnerUserId});

        // Удаление пользователя не должно уносить общие представления, которые
        // он когда-то завёл: ими пользуется банк, а не он один.
        b.HasOne(x => x.OwnerUser).WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.OrgUnit).WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
