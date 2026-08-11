using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementAuthorityMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dictionary_procurement_method",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<int>(type: "integer", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    short_title_ru = table.Column<string>(type: "text", nullable: false),
                    min_proposals = table.Column<int>(type: "integer", nullable: false),
                    requires_justification = table.Column<bool>(type: "boolean", nullable: false),
                    requires_publication = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_procurement_method", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procurement_parameter",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    source_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_parameter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procurement_authority_matrix_rule",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    method_id = table.Column<int>(type: "integer", nullable: false),
                    is_affiliated = table.Column<bool>(type: "boolean", nullable: false),
                    min_base = table.Column<int>(type: "integer", nullable: false),
                    max_base = table.Column<int>(type: "integer", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    max_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    approval_chain_ru = table.Column<string>(type: "text", nullable: false),
                    commission_required = table.Column<bool>(type: "boolean", nullable: false),
                    commission_size = table.Column<int>(type: "integer", nullable: true),
                    commission_min_board_members = table.Column<int>(type: "integer", nullable: true),
                    approval_authority = table.Column<int>(type: "integer", nullable: false),
                    commission_note_ru = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_authority_matrix_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_authority_matrix_rule_procurement_methods_metho",
                        column: x => x.method_id,
                        principalTable: "dictionary_procurement_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "dictionary_procurement_method",
                columns: new[] { "id", "code", "created_at", "is_active", "min_proposals", "requires_justification", "requires_publication", "short_title_ru", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, true, false, "Прямое", "Direct contract", "Түз келишим түзүү", "Прямое заключение договора", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, false, false, "Простая", "Simple procurement (RFQ)", "Жөнөкөй сатып алуу", "Простая закупка (запрос ценовых предложений)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, false, true, "Конкурс", "Open tender", "Чектелбеген катышуу менен конкурс", "Конкурс с неограниченным участием", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 0, true, false, "Конкурс огр.", "Limited tender", "Чектелген катышуу менен конкурс", "Конкурс с ограниченным участием", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "procurement_parameter",
                columns: new[] { "id", "code", "created_at", "source_note", "title_ru", "unit", "updated_at", "value" },
                values: new object[,]
                {
                    { 1, "BalanceAssets", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Заполняется по данным УБУиО на отчётную дату", "Балансовая стоимость активов", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42000000000m },
                    { 2, "Nsk", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Заполняется по данным УБУиО на отчётную дату", "Чистый собственный капитал (ЧСК)", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6500000000m },
                    { 3, "ProtocolThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-10; целевое значение — открытый вопрос В-4 (50 000 против 100 000)", "Порог обязательного протокола закупки", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 50000m },
                    { 4, "CommissionAccountantThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-14", "Порог включения сотрудника УБУиО в комиссию", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5000000m },
                    { 5, "CommissionBoardChairThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-14: председатель — член Правления, не курирующий инициирующее СП", "Порог назначения председателем комиссии члена Правления", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3000000m }
                });

            migrationBuilder.InsertData(
                table: "procurement_authority_matrix_rule",
                columns: new[] { "id", "approval_authority", "approval_chain_ru", "commission_min_board_members", "commission_note_ru", "commission_required", "commission_size", "created_at", "is_active", "is_affiliated", "max_base", "max_value", "method_id", "min_base", "min_value", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { 1, 1, "Куратор", null, "Комиссия не создаётся; отбор по не менее чем 3 КП с наименьшей стоимостью", false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 1, 500000m, 2, 1, 1m, 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 0, "Куратор", null, "Комиссия не создаётся; обязательно обоснование применения метода (п. 6.6 Положения)", false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 1, 500000m, 1, 1, 100000m, 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 2, "Куратор + Правление", null, "Комиссия не создаётся; расход утверждает Правление", false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 1, null, 1, 1, 500000m, 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 2, "Куратор", null, "Комиссия из 5 членов", true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 2, 20m, 3, 1, 500000m, 40, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, 3, "Куратор + Правление", 2, "Комиссия из 5 членов, не менее 2 членов Правления", true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 2, 50m, 3, 2, 20m, 50, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, 4, "Куратор + Правление + Совет директоров", 2, "Комиссия из 5 членов, не менее 2 членов Правления", true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 2, null, 3, 2, 50m, 60, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, 3, "Куратор + Правление", null, "Прямое заключение до 14% ЧСК; решение — Совет директоров", false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, 3, 14m, 1, 3, 0m, 70, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, 3, "Куратор + Правление", 2, "Тендерная комиссия из 5 членов, не менее 2 членов Правления", true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, 3, 14m, 3, 3, 1m, 80, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, 4, "Куратор + Правление", 2, "Свыше 14% ЧСК — решение Общего собрания акционеров", true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, 3, null, 3, 3, 14m, 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_procurement_method_code",
                table: "dictionary_procurement_method",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_authority_matrix_rule_method_id_is_affiliated_s",
                table: "procurement_authority_matrix_rule",
                columns: new[] { "method_id", "is_affiliated", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_parameter_code",
                table: "procurement_parameter",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_authority_matrix_rule");

            migrationBuilder.DropTable(
                name: "procurement_parameter");

            migrationBuilder.DropTable(
                name: "dictionary_procurement_method");
        }
    }
}
