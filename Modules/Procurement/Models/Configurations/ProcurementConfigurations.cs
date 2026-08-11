using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class ProcurementMethodConfiguration : IEntityTypeConfiguration<ProcurementMethod>
{
    public void Configure(EntityTypeBuilder<ProcurementMethod> b)
    {
        b.ToTable("dictionary_procurement_method");
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasConversion<int>();

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        b.HasData(
            new
            {
                Id = 1, Code = ProcurementMethodCode.Direct,
                TitleRu = "Прямое заключение договора", TitleEn = "Direct contract", TitleKg = "Түз келишим түзүү",
                ShortTitleRu = "Прямое", MinProposals = 0,
                RequiresJustification = true, RequiresPublication = false,
                IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 2, Code = ProcurementMethodCode.Simple,
                TitleRu = "Простая закупка (запрос ценовых предложений)", TitleEn = "Simple procurement (RFQ)", TitleKg = "Жөнөкөй сатып алуу",
                ShortTitleRu = "Простая", MinProposals = 3,
                RequiresJustification = false, RequiresPublication = false,
                IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 3, Code = ProcurementMethodCode.TenderOpen,
                TitleRu = "Конкурс с неограниченным участием", TitleEn = "Open tender", TitleKg = "Чектелбеген катышуу менен конкурс",
                ShortTitleRu = "Конкурс", MinProposals = 0,
                RequiresJustification = false, RequiresPublication = true,
                IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 4, Code = ProcurementMethodCode.TenderLimited,
                TitleRu = "Конкурс с ограниченным участием", TitleEn = "Limited tender", TitleKg = "Чектелген катышуу менен конкурс",
                ShortTitleRu = "Конкурс огр.", MinProposals = 0,
                RequiresJustification = true, RequiresPublication = false,
                IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            }
        );
    }
}

public class ProcurementParameterConfiguration : IEntityTypeConfiguration<ProcurementParameter>
{
    public void Configure(EntityTypeBuilder<ProcurementParameter> b)
    {
        b.ToTable("procurement_parameter");
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Value).HasPrecision(18, 2);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Баланс и ЧСК — демонстрационные значения до выгрузки из отчётности УБУиО:
        // от них считаются пороги конкурса (20% / 50% активов) и аффилированных сделок (1% / 14% ЧСК).
        b.HasData(
            new
            {
                Id = 1, Code = "BalanceAssets", TitleRu = "Балансовая стоимость активов", Value = 42_000_000_000m,
                Unit = "сом", SourceNote = "Заполняется по данным УБУиО на отчётную дату",
                CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 2, Code = "Nsk", TitleRu = "Чистый собственный капитал (ЧСК)", Value = 6_500_000_000m,
                Unit = "сом", SourceNote = "Заполняется по данным УБУиО на отчётную дату",
                CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 3, Code = "ProtocolThreshold", TitleRu = "Порог обязательного протокола закупки", Value = 50_000m,
                Unit = "сом", SourceNote = "PRC-10; целевое значение — открытый вопрос В-4 (50 000 против 100 000)",
                CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 4, Code = "CommissionAccountantThreshold", TitleRu = "Порог включения сотрудника УБУиО в комиссию", Value = 5_000_000m,
                Unit = "сом", SourceNote = "PRC-14",
                CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 5, Code = "CommissionBoardChairThreshold", TitleRu = "Порог назначения председателем комиссии члена Правления", Value = 3_000_000m,
                Unit = "сом", SourceNote = "PRC-14: председатель — член Правления, не курирующий инициирующее СП",
                CreatedAt = seedDate, UpdatedAt = seedDate,
            }
        );
    }
}

public class AuthorityMatrixRuleConfiguration : IEntityTypeConfiguration<AuthorityMatrixRule>
{
    public void Configure(EntityTypeBuilder<AuthorityMatrixRule> b)
    {
        b.ToTable("procurement_authority_matrix_rule");
        b.Property(x => x.MinValue).HasPrecision(18, 2);
        b.Property(x => x.MaxValue).HasPrecision(18, 2);
        b.Property(x => x.MinBase).HasConversion<int>();
        b.Property(x => x.MaxBase).HasConversion<int>();
        b.Property(x => x.ApprovalAuthority).HasConversion<int>();

        b.HasOne(x => x.Method)
            .WithMany()
            .HasForeignKey(x => x.MethodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Диапазоны ищутся по способу, аффилированности и сумме — индекс под этот запрос.
        b.HasIndex(x => new { x.MethodId, x.IsAffiliated, x.SortOrder });

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Начальные значения — Матрица полномочий Положения о закупках
        // (утв. Правлением, протокол № 23(8) от 28.05.2024, с изм. 28.03.2025 и 28.04.2025).
        b.HasData(
            // --- обычные закупки ---
            new
            {
                Id = 1, MethodId = 2, IsAffiliated = false, MinBase = ThresholdBase.Absolute, MaxBase = ThresholdBase.Absolute,
                MinValue = (decimal?)1m, MaxValue = (decimal?)500_000m,
                ApprovalChainRu = "Куратор", CommissionRequired = false,
                CommissionSize = (int?)null, CommissionMinBoardMembers = (int?)null,
                ApprovalAuthority = ApprovalAuthority.Curator,
                CommissionNoteRu = "Комиссия не создаётся; отбор по не менее чем 3 КП с наименьшей стоимостью",
                SortOrder = 10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 2, MethodId = 1, IsAffiliated = false, MinBase = ThresholdBase.Absolute, MaxBase = ThresholdBase.Absolute,
                MinValue = (decimal?)100_000m, MaxValue = (decimal?)500_000m,
                ApprovalChainRu = "Куратор", CommissionRequired = false,
                CommissionSize = (int?)null, CommissionMinBoardMembers = (int?)null,
                ApprovalAuthority = ApprovalAuthority.None,
                CommissionNoteRu = "Комиссия не создаётся; обязательно обоснование применения метода (п. 6.6 Положения)",
                SortOrder = 20, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 3, MethodId = 1, IsAffiliated = false, MinBase = ThresholdBase.Absolute, MaxBase = ThresholdBase.Absolute,
                MinValue = (decimal?)500_000m, MaxValue = (decimal?)null,
                ApprovalChainRu = "Куратор + Правление", CommissionRequired = false,
                CommissionSize = (int?)null, CommissionMinBoardMembers = (int?)null,
                ApprovalAuthority = ApprovalAuthority.Board,
                CommissionNoteRu = "Комиссия не создаётся; расход утверждает Правление",
                SortOrder = 30, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 4, MethodId = 3, IsAffiliated = false, MinBase = ThresholdBase.Absolute, MaxBase = ThresholdBase.PercentOfAssets,
                MinValue = (decimal?)500_000m, MaxValue = (decimal?)20m,
                ApprovalChainRu = "Куратор", CommissionRequired = true,
                CommissionSize = (int?)5, CommissionMinBoardMembers = (int?)null,
                ApprovalAuthority = ApprovalAuthority.Board,
                CommissionNoteRu = "Комиссия из 5 членов",
                SortOrder = 40, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 5, MethodId = 3, IsAffiliated = false, MinBase = ThresholdBase.PercentOfAssets, MaxBase = ThresholdBase.PercentOfAssets,
                MinValue = (decimal?)20m, MaxValue = (decimal?)50m,
                ApprovalChainRu = "Куратор + Правление", CommissionRequired = true,
                CommissionSize = (int?)5, CommissionMinBoardMembers = (int?)2,
                ApprovalAuthority = ApprovalAuthority.SupervisoryBoard,
                CommissionNoteRu = "Комиссия из 5 членов, не менее 2 членов Правления",
                SortOrder = 50, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 6, MethodId = 3, IsAffiliated = false, MinBase = ThresholdBase.PercentOfAssets, MaxBase = ThresholdBase.PercentOfAssets,
                MinValue = (decimal?)50m, MaxValue = (decimal?)null,
                ApprovalChainRu = "Куратор + Правление + Совет директоров", CommissionRequired = true,
                CommissionSize = (int?)5, CommissionMinBoardMembers = (int?)2,
                ApprovalAuthority = ApprovalAuthority.Shareholders,
                CommissionNoteRu = "Комиссия из 5 членов, не менее 2 членов Правления",
                SortOrder = 60, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },

            // --- сделки с аффилированными лицами: шкала от ЧСК ---
            new
            {
                Id = 7, MethodId = 1, IsAffiliated = true, MinBase = ThresholdBase.PercentOfNsk, MaxBase = ThresholdBase.PercentOfNsk,
                MinValue = (decimal?)0m, MaxValue = (decimal?)14m,
                ApprovalChainRu = "Куратор + Правление", CommissionRequired = false,
                CommissionSize = (int?)null, CommissionMinBoardMembers = (int?)null,
                ApprovalAuthority = ApprovalAuthority.SupervisoryBoard,
                CommissionNoteRu = "Прямое заключение до 14% ЧСК; решение — Совет директоров",
                SortOrder = 70, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 8, MethodId = 3, IsAffiliated = true, MinBase = ThresholdBase.PercentOfNsk, MaxBase = ThresholdBase.PercentOfNsk,
                MinValue = (decimal?)1m, MaxValue = (decimal?)14m,
                ApprovalChainRu = "Куратор + Правление", CommissionRequired = true,
                CommissionSize = (int?)5, CommissionMinBoardMembers = (int?)2,
                ApprovalAuthority = ApprovalAuthority.SupervisoryBoard,
                CommissionNoteRu = "Тендерная комиссия из 5 членов, не менее 2 членов Правления",
                SortOrder = 80, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            },
            new
            {
                Id = 9, MethodId = 3, IsAffiliated = true, MinBase = ThresholdBase.PercentOfNsk, MaxBase = ThresholdBase.PercentOfNsk,
                MinValue = (decimal?)14m, MaxValue = (decimal?)null,
                ApprovalChainRu = "Куратор + Правление", CommissionRequired = true,
                CommissionSize = (int?)5, CommissionMinBoardMembers = (int?)2,
                ApprovalAuthority = ApprovalAuthority.Shareholders,
                CommissionNoteRu = "Свыше 14% ЧСК — решение Общего собрания акционеров",
                SortOrder = 90, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate,
            }
        );
    }
}
