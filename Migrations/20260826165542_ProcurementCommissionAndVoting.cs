using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Приведение модуля закупок к Положению о закупках товаров, работ и услуг
    /// (утв. Правлением, протокол № 38(3) от 30.06.2026).
    ///
    /// Удаление параметров 4 и 5 намеренное. Это были «порог включения УБУиО в
    /// комиссию» (5 млн) и «порог назначения председателем члена Правления» (3 млн).
    /// В Положении таких порогов нет: п. 120 требует и того, и другого безусловно.
    /// Пока пороги существовали, конкурсы ниже этих сумм проходили составом,
    /// который Положение не допускает.
    ///
    /// Правило 2 матрицы (прямое заключение, 100 000 – 500 000): расход утверждает
    /// куратор инициатора, а стояло «утверждение не требуется».
    /// Правило 6 (конкурс от 50% активов): согласуют куратор и Правление; Совет
    /// директоров из цепочки убран — он утверждает расход по диапазону 20–50%.
    ///
    /// Новые таблицы — поимённое голосование комиссии (п. 24.2–24.3) и переносы
    /// заседания: решения принимаются очно, и у даты заседания должна быть история.
    /// </remarks>
    public partial class ProcurementCommissionAndVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "procurement_parameter",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "procurement_parameter",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.AddColumn<DateOnly>(
                name: "meeting_date",
                table: "procurement_tender",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legal",
                table: "procurement_commission_member",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_security",
                table: "procurement_commission_member",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "procurement_commission_vote",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bid_id = table.Column<int>(type: "integer", nullable: false),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    choice = table.Column<int>(type: "integer", nullable: false),
                    recorded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_commission_vote", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_commission_vote_procurement_commission_member_m",
                        column: x => x.member_id,
                        principalTable: "procurement_commission_member",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_commission_vote_tender_bids_bid_id",
                        column: x => x.bid_id,
                        principalTable: "procurement_tender_bid",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_commission_vote_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_tender_meeting_change",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tender_id = table.Column<int>(type: "integer", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    by_user_id = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_tender_meeting_change", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_tender_meeting_change_procurement_tender_tender",
                        column: x => x.tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_tender_meeting_change_users_by_user_id",
                        column: x => x.by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "procurement_authority_matrix_rule",
                keyColumn: "id",
                keyValue: 2,
                column: "approval_authority",
                value: 1);

            migrationBuilder.UpdateData(
                table: "procurement_authority_matrix_rule",
                keyColumn: "id",
                keyValue: 6,
                column: "approval_chain_ru",
                value: "Куратор + Правление");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_commission_vote_bid_id_member_id",
                table: "procurement_commission_vote",
                columns: new[] { "bid_id", "member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_commission_vote_member_id",
                table: "procurement_commission_vote",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_commission_vote_recorded_by_user_id",
                table: "procurement_commission_vote",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_meeting_change_by_user_id",
                table: "procurement_tender_meeting_change",
                column: "by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_meeting_change_tender_id_at",
                table: "procurement_tender_meeting_change",
                columns: new[] { "tender_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_commission_vote");

            migrationBuilder.DropTable(
                name: "procurement_tender_meeting_change");

            migrationBuilder.DropColumn(
                name: "meeting_date",
                table: "procurement_tender");

            migrationBuilder.DropColumn(
                name: "is_legal",
                table: "procurement_commission_member");

            migrationBuilder.DropColumn(
                name: "is_security",
                table: "procurement_commission_member");

            migrationBuilder.UpdateData(
                table: "procurement_authority_matrix_rule",
                keyColumn: "id",
                keyValue: 2,
                column: "approval_authority",
                value: 0);

            migrationBuilder.UpdateData(
                table: "procurement_authority_matrix_rule",
                keyColumn: "id",
                keyValue: 6,
                column: "approval_chain_ru",
                value: "Куратор + Правление + Совет директоров");

            migrationBuilder.InsertData(
                table: "procurement_parameter",
                columns: new[] { "id", "code", "created_at", "source_note", "title_ru", "unit", "updated_at", "value" },
                values: new object[,]
                {
                    { 4, "CommissionAccountantThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-14", "Порог включения сотрудника УБУиО в комиссию", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5000000m },
                    { 5, "CommissionBoardChairThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-14: председатель — член Правления, не курирующий инициирующее СП", "Порог назначения председателем комиссии члена Правления", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3000000m }
                });
        }
    }
}
