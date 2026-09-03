using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Users.Models.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");
        builder.Property(x => x.PermissionCodes).HasColumnType("integer[]").HasColumnName("permission_codes");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Членство в коллегиальном органе — не право доступа, а состав органа:
        // человек либо входит в Правление, либо нет, и администратор системы в
        // него не входит. Пока эти признаки лежали в общем перечне, «все права»
        // делали членами Правления айтишников и редакторов ВНД — в поле «Кому»
        // служебной записки предлагались они, а настоящих членов там не было.
        var membershipFlags = new[]
        {
            PermissionCode.MemberOfBoard,
            PermissionCode.MemberOfKpa,
            PermissionCode.MemberOfCreditCommittee,
        };

        var allPermissions = Enum.GetValues<PermissionCode>()
            .Where(p => !membershipFlags.Contains(p))
            .Select(p => (int)p)
            .ToArray();

        var ordinaryUserPermissions = new[]
        {
            (int)PermissionCode.ViewVnd,
            (int)PermissionCode.ViewLimitedStatistics
        };

        var vndEditorPermissions = new[]
        {
            (int)PermissionCode.ViewVndActualizationPage,
            (int)PermissionCode.ActualizeVndWithApprovalByRequest,
            (int)PermissionCode.ActualizeVndWithoutApprovalByRequest,
            (int)PermissionCode.CreateVndWithApproval,
            (int)PermissionCode.ManageGroups,
            (int)PermissionCode.ManageVndDictionaries,
            (int)PermissionCode.ViewVnd,
            (int)PermissionCode.ExportVnd,
            (int)PermissionCode.ExportFullStatisticsReport,
            (int)PermissionCode.ViewFullStatistics,
            (int)PermissionCode.ActAsApprover,
            (int)PermissionCode.EditVndRequisites,
            (int)PermissionCode.ViewVndRegistryExtended
        };

        builder.HasData(
            new
            {
                Id = 1,
                TitleRu = "Администратор",
                TitleEn = "Administrator",
                TitleKg = "Администратор",
                PermissionCodes = allPermissions,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new
            {
                Id = 2,
                TitleRu = "Рядовой пользователь",
                TitleEn = "Regular User",
                TitleKg = "Жөнөкөй колдонуучу",
                PermissionCodes = ordinaryUserPermissions,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new
            {
                Id = 3,
                TitleRu = "Редактор ВНД",
                TitleEn = "VND Editor",
                TitleKg = "ВНД редактору",
                PermissionCodes = vndEditorPermissions,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new
            {
                Id = 4,
                TitleRu = "Главный редактор ВНД",
                TitleEn = "Chief VND Editor",
                TitleKg = "ВНД башкы редактору",
                PermissionCodes = allPermissions,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        );
    }
}