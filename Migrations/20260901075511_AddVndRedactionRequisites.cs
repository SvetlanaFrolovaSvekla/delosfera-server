using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndRedactionRequisites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "adoption_code",
                table: "vnd_redaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "adoption_date",
                table: "vnd_redaction",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "curator_developer_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "developer_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_date",
                table: "vnd_redaction",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "organ_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "period",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "secrecy_level_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "vnd_redaction_keyword",
                columns: table => new
                {
                    keyword_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_keyword", x => new { x.keyword_id, x.vnd_redaction_id });
                    table.ForeignKey(
                        name: "fk_vnd_redaction_keyword_dictionary_keyword_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "dictionary_keyword",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_keyword_vnd_redaction_vnd_redaction_id",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_redaction_responsible_executor",
                columns: table => new
                {
                    organization_unit_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_responsible_executor", x => new { x.organization_unit_id, x.vnd_redaction_id });
                    table.ForeignKey(
                        name: "fk_vnd_redaction_responsible_executor_dictionary_organization_",
                        column: x => x.organization_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_responsible_executor_vnd_redaction_vnd_redact",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_redaction_rubric",
                columns: table => new
                {
                    rubric_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_rubric", x => new { x.rubric_id, x.vnd_redaction_id });
                    table.ForeignKey(
                        name: "fk_vnd_redaction_rubric_dictionary_rubric_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "dictionary_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_rubric_vnd_redaction_vnd_redaction_id",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54 });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_curator_developer_id",
                table: "vnd_redaction",
                column: "curator_developer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_developer_id",
                table: "vnd_redaction",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_organ_id",
                table: "vnd_redaction",
                column: "organ_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_secrecy_level_id",
                table: "vnd_redaction",
                column: "secrecy_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_keyword_vnd_redaction_id",
                table: "vnd_redaction_keyword",
                column: "vnd_redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_responsible_executor_vnd_redaction_id",
                table: "vnd_redaction_responsible_executor",
                column: "vnd_redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_rubric_vnd_redaction_id",
                table: "vnd_redaction_rubric",
                column: "vnd_redaction_id");

            // --- Бэкфилл реквизитов уже существующих редакций значением с документа 
            migrationBuilder.Sql(@"
                UPDATE vnd_redaction r
                SET organ_id = d.organ_id,
                    developer_id = d.developer_id,
                    curator_developer_id = d.curator_developer_id,
                    secrecy_level_id = d.secrecy_level_id,
                    period = d.period,
                    adoption_date = d.adoption_date,
                    adoption_code = d.adoption_code,
                    effective_date = d.effective_date
                FROM vnd_document d
                WHERE r.vnd_id = d.id;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO vnd_redaction_responsible_executor (vnd_redaction_id, organization_unit_id)
                SELECT r.id, e.organization_unit_id
                FROM vnd_redaction r
                JOIN vnd_responsible_executor e ON e.vnd_id = r.vnd_id;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO vnd_redaction_keyword (vnd_redaction_id, keyword_id)
                SELECT r.id, k.keyword_id
                FROM vnd_redaction r
                JOIN vnd_keyword k ON k.vnd_id = r.vnd_id;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO vnd_redaction_rubric (vnd_redaction_id, rubric_id)
                SELECT r.id, ru.rubric_id
                FROM vnd_redaction r
                JOIN vnd_rubric ru ON ru.vnd_id = r.vnd_id;
            ");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_dictionary_approval_body_organ_id",
                table: "vnd_redaction",
                column: "organ_id",
                principalTable: "dictionary_approval_body",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_dictionary_organization_unit_developer_id",
                table: "vnd_redaction",
                column: "developer_id",
                principalTable: "dictionary_organization_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_dictionary_security_level_secrecy_level_id",
                table: "vnd_redaction",
                column: "secrecy_level_id",
                principalTable: "dictionary_security_level",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_users_curator_developer_id",
                table: "vnd_redaction",
                column: "curator_developer_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_dictionary_approval_body_organ_id",
                table: "vnd_redaction");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_dictionary_organization_unit_developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_dictionary_security_level_secrecy_level_id",
                table: "vnd_redaction");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_users_curator_developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropTable(
                name: "vnd_redaction_keyword");

            migrationBuilder.DropTable(
                name: "vnd_redaction_responsible_executor");

            migrationBuilder.DropTable(
                name: "vnd_redaction_rubric");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_curator_developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_organ_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_secrecy_level_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "adoption_code",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "adoption_date",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "curator_developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "developer_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "effective_date",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "organ_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "period",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "secrecy_level_id",
                table: "vnd_redaction");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53 });
        }
    }
}