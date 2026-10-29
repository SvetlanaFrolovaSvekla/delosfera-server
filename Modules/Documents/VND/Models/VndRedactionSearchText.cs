using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Извлечённый из Word-файлов текст редакции — для расширенного поиска реестра ВНД по полю
/// «Текст редакции» (VndSearchRequest.RevisionText). Одна строка на редакцию: тексты на всех
/// языках (RU/KG/EN) сложены вместе.
///
/// Вынесено в отдельную таблицу, а не в колонку vnd_redaction, намеренно: реестр грузит
/// редакции всех документов через Include(x => x.Redactions), и колонка с полным текстом
/// каждой редакции тянулась бы в память на каждое открытие реестра.
///
/// Заполняется VndLegacyLinkIndexer в том же проходе, что и легаси-ссылки (тот же отпечаток
/// файлов VndRedaction.LegacyLinksScanKey — меняется файл, переизвлекается и текст).
/// </summary>
public class VndRedactionSearchText
{
    public int RedactionId { get; set; }
    public VndRedaction? Redaction { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime IndexedAt { get; set; }
}

public class VndRedactionSearchTextConfiguration : IEntityTypeConfiguration<VndRedactionSearchText>
{
    public void Configure(EntityTypeBuilder<VndRedactionSearchText> b)
    {
        b.ToTable("vnd_redaction_search_text");
        b.HasKey(x => x.RedactionId).HasName("pk_vnd_redaction_search_text");
        b.Property(x => x.RedactionId).ValueGeneratedNever();
        b.Property(x => x.Text).HasColumnType("text").IsRequired();

        // Триграммный GIN-индекс (pg_trgm) под поиск «Текст редакции»: ILIKE '%фраза%' без него
        // перебирал тексты всех редакций целиком (до 2 МБ каждый) на каждый запрос реестра, с ним
        // база сразу находит редакции-кандидаты и перепроверяет только их. Работает для фраз от 3
        // символов. Создаётся миграцией AddVndRedactionSearchTextTrgmIndex (там же CREATE EXTENSION).
        b.HasIndex(x => x.Text)
            .HasDatabaseName("ix_vnd_redaction_search_text_text_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        b.HasOne(x => x.Redaction)
            .WithOne()
            .HasForeignKey<VndRedactionSearchText>(x => x.RedactionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vnd_redaction_search_text_vnd_redaction_redaction_id");
    }
}
