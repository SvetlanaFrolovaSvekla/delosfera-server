using delosfera_server.Modules.Documents.VND.DTO.Request;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndDocumentConfiguration : IEntityTypeConfiguration<VndDocument>
{
    public void Configure(EntityTypeBuilder<VndDocument> builder)
    {
        builder.ToTable("vnd_document");

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Developer).WithMany().HasForeignKey(x => x.DeveloperId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CuratorDeveloper).WithMany().HasForeignKey(x => x.CuratorDeveloperId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Organ).WithMany().HasForeignKey(x => x.OrganId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SecrecyLevel).WithMany().HasForeignKey(x => x.SecrecyLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ActualizationResponsibleUser)
            .WithMany()
            .HasForeignKey(x => x.ActualizationResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Redactions)
            .WithOne(x => x.Vnd)
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Ответственные исполнители (many-to-many с OrganizationUnit) ───
        builder.HasMany(x => x.ResponsibleExecutors)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_responsible_executor",
                j => j.HasOne<Dictionaries.Models.OrganizationUnit>().WithMany().HasForeignKey("OrganizationUnitId"),
                j => j.HasOne<VndDocument>().WithMany().HasForeignKey("VndId"),
                j =>
                {
                    j.ToTable("vnd_responsible_executor");
                    j.HasData(
                        new { VndId = 1, OrganizationUnitId = 26 }, // ВНД-062 — Управление рисками
                        new { VndId = 2, OrganizationUnitId = 26 }, // ВНД-084 — Управление рисками
                        new { VndId = 2, OrganizationUnitId = 8 }, // ВНД-084 — Управление кредитования
                        new { VndId = 3, OrganizationUnitId = 38 }, // ВНД-011 — УБУиО
                        new { VndId = 4, OrganizationUnitId = 32 }, // ВНД-201 — Управление ЧР
                        new { VndId = 5, OrganizationUnitId = 4 } // ВНД-037 — Управление казначейских операций
                    );
                });

        // ─── Ключевые слова (many-to-many с Keyword) ───
        builder.HasMany(x => x.Keywords)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_keyword",
                j => j.HasOne<Dictionaries.Models.Keyword>().WithMany().HasForeignKey("KeywordId"),
                j => j.HasOne<VndDocument>().WithMany().HasForeignKey("VndId"),
                j =>
                {
                    j.ToTable("vnd_keyword");
                    j.HasData(
                        new { VndId = 1, KeywordId = 6 }, // ВНД-062 — Ответственность
                        new { VndId = 1, KeywordId = 12 }, // ВНД-062 — Матрица
                        new { VndId = 2, KeywordId = 11 }, // ВНД-084 — Безопасность
                        new { VndId = 3, KeywordId = 8 }, // ВНД-011 — Расход
                        new { VndId = 4, KeywordId = 6 } // ВНД-201 — Ответственность
                    );
                });

        // ─── Рубрики (many-to-many с Rubric) ───
        builder.HasMany(x => x.Rubrics)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_rubric",
                j => j.HasOne<Dictionaries.Models.Rubric>().WithMany().HasForeignKey("RubricId"),
                j => j.HasOne<VndDocument>().WithMany().HasForeignKey("VndId"),
                j =>
                {
                    j.ToTable("vnd_rubric");
                    j.HasData(
                        new { VndId = 1, RubricId = 5 },
                        new { VndId = 2, RubricId = 5 },
                        new { VndId = 3, RubricId = 15 },
                        new { VndId = 4, RubricId = 7 },
                        new { VndId = 5, RubricId = 11 },
                        new { VndId = 3, RubricId = 11 }
                    );
                });

        // ─── Группы доступа (many-to-many с UserGroup) ───
        builder.HasMany(x => x.UserGroups)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_user_group",
                j => j.HasOne<Dictionaries.Models.UserGroup>().WithMany().HasForeignKey("UserGroupId"),
                j => j.HasOne<VndDocument>().WithMany().HasForeignKey("VndId"),
                j => { j.ToTable("vnd_user_group"); });

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new
            {
                Id = 1, Code = "10062", TitleRu = "Порядок работы с обеспечением (залогами)",
                Status = VndStatus.Consolidation,
                TypeId = 13, DeveloperId = 26, CuratorDeveloperId = (int?)1, OrganId = 3,
                AdoptionDate = new DateOnly(2023, 2, 9), AdoptionCode = "пр. №4(2)",
                EffectiveDate = (DateOnly?)new DateOnly(2023, 2, 16),
                RequisitesChangedDate = (DateOnly?)new DateOnly(2026, 1, 12),
                RevisionChangedDate = (DateOnly?)new DateOnly(2026, 7, 20),
                CancelDate = (DateOnly?)null, CancelCode = (string?)null, CancelReason = (string?)null,
                ArchivedDate = (DateOnly?)null,
                DueActualizationDate = (DateOnly?)new DateOnly(2026, 8, 9),
                LastActualizationDate = (DateOnly?)new DateOnly(2025, 8, 9),
                LastActualizationHadChanges = true,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 3,
                CreatedByUserId = (int?)14,
                CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 2, Code = "10084", TitleRu = "Политика управления кредитными рисками",
                Status = VndStatus.Active,
                TypeId = 11, DeveloperId = 26, CuratorDeveloperId = (int?)1, OrganId = 7,
                AdoptionDate = new DateOnly(2021, 3, 14), AdoptionCode = "пр. №9(1)",
                EffectiveDate = (DateOnly?)new DateOnly(2021, 4, 1),
                RequisitesChangedDate = (DateOnly?)new DateOnly(2025, 5, 5),
                RevisionChangedDate = (DateOnly?)new DateOnly(2026, 6, 18),
                CancelDate = (DateOnly?)null, CancelCode = (string?)null, CancelReason = (string?)null,
                ArchivedDate = (DateOnly?)null,
                DueActualizationDate = (DateOnly?)new DateOnly(2026, 7, 22),
                LastActualizationDate = (DateOnly?)new DateOnly(2025, 7, 22),
                LastActualizationHadChanges = false,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 2,
                CreatedByUserId = (int?)14,
                CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 3, Code = "10011", TitleRu = "Регламент кассовых операций",
                Status = VndStatus.OnActualization,
                TypeId = 17, DeveloperId = 38, CuratorDeveloperId = (int?)11, OrganId = 3,
                AdoptionDate = new DateOnly(2020, 1, 20), AdoptionCode = "пр. №2(5)",
                EffectiveDate = (DateOnly?)new DateOnly(2020, 2, 1),
                RequisitesChangedDate = (DateOnly?)new DateOnly(2024, 10, 10),
                RevisionChangedDate = (DateOnly?)new DateOnly(2026, 7, 1),
                CancelDate = (DateOnly?)null, CancelCode = (string?)null, CancelReason = (string?)null,
                ArchivedDate = (DateOnly?)null,
                DueActualizationDate = (DateOnly?)new DateOnly(2026, 7, 25),
                LastActualizationDate = (DateOnly?)new DateOnly(2025, 7, 25),
                LastActualizationHadChanges = true,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 1,
                CreatedByUserId = (int?)7,
                CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 4, Code = "10201", TitleRu = "Кодекс корпоративной этики",
                Status = VndStatus.Active,
                TypeId = 5, DeveloperId = 32, CuratorDeveloperId = (int?)9, OrganId = 2,
                AdoptionDate = new DateOnly(2019, 5, 5), AdoptionCode = "пр. ОСА-1",
                EffectiveDate = (DateOnly?)new DateOnly(2019, 6, 1),
                RequisitesChangedDate = (DateOnly?)new DateOnly(2024, 3, 1),
                RevisionChangedDate = (DateOnly?)new DateOnly(2025, 9, 14),
                CancelDate = (DateOnly?)null, CancelCode = (string?)null, CancelReason = (string?)null,
                ArchivedDate = (DateOnly?)null,
                DueActualizationDate = (DateOnly?)new DateOnly(2026, 9, 28),
                LastActualizationDate = (DateOnly?)new DateOnly(2025, 9, 28),
                LastActualizationHadChanges = false,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 1,
                CreatedByUserId = (int?)15,
                CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 5, Code = "10037", TitleRu = "Регламент управления ликвидностью (ред. 2019)",
                Status = VndStatus.Archived,
                TypeId = 17, DeveloperId = 4, CuratorDeveloperId = (int?)12, OrganId = 3,
                AdoptionDate = new DateOnly(2019, 1, 10), AdoptionCode = "пр. №1(4)",
                EffectiveDate = (DateOnly?)new DateOnly(2019, 2, 1),
                RequisitesChangedDate = (DateOnly?)new DateOnly(2019, 2, 1),
                RevisionChangedDate = (DateOnly?)new DateOnly(2019, 2, 1),
                CancelDate = (DateOnly?)new DateOnly(2024, 3, 14), CancelCode = "пр. №8(3)",
                CancelReason = "Заменён новой редакцией ВНД-037 v4.1",
                ArchivedDate = (DateOnly?)new DateOnly(2024, 3, 20),
                DueActualizationDate = (DateOnly?)null,
                LastActualizationDate = (DateOnly?)null,
                LastActualizationHadChanges = false,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 2,
                CreatedByUserId = (int?)12,
                CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 6, Code = "10210", TitleRu = "Тест",
                Status = VndStatus.Draft,
                TypeId = 1, DeveloperId = 33, CuratorDeveloperId = (int?)null, OrganId = 2,
                AdoptionDate = (DateOnly?)null, AdoptionCode = (string?)null,
                EffectiveDate = (DateOnly?)null,
                RequisitesChangedDate = (DateOnly?)null,
                RevisionChangedDate = (DateOnly?)null,
                CancelDate = (DateOnly?)null, CancelCode = (string?)null, CancelReason = (string?)null,
                ArchivedDate = (DateOnly?)null,
                DueActualizationDate = (DateOnly?)new DateOnly(2027, 8, 4),
                LastActualizationDate = (DateOnly?)new DateOnly(2026, 8, 4),
                LastActualizationHadChanges = false,
                Period = ActualizationPeriod.Annual,
                ActualizationResponsibleUserId = (int?)null,
                ActualizationRequiresApproval = false,
                ActualizationShiftNextPeriod = false,
                SecrecyLevelId = 3,
                CreatedByUserId = (int?)17,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }
        );
    }
}