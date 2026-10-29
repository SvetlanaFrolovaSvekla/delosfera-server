using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Ускорение поиска реестра ВНД по полю «Текст редакции»: триграммный GIN-индекс (pg_trgm)
    /// на vnd_redaction_search_text.text. Поиск идёт через ILIKE '%фраза%' — без индекса PostgreSQL
    /// на каждый запрос перебирал тексты всех редакций целиком; с индексом сразу находит редакции,
    /// где встречаются все триграммы фразы, и перепроверяет только их. Индекс используется для фраз
    /// от 3 символов (короче — прежний полный перебор, результат тот же).
    ///
    /// pg_trgm входит в стандартную поставку PostgreSQL (contrib) и с PG 13 — «доверенное»
    /// расширение: создать его может владелец базы. Индекс строится один раз при применении миграции
    /// (на больших текстах — до нескольких минут). Миграция написана вручную (без Designer-файла),
    /// снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261029120000_AddVndRedactionSearchTextTrgmIndex")]
    public partial class AddVndRedactionSearchTextTrgmIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_vnd_redaction_search_text_text_trgm " +
                "ON vnd_redaction_search_text USING gin (text gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Расширение не удаляем: им могут пользоваться и другие индексы.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_vnd_redaction_search_text_text_trgm;");
        }
    }
}
