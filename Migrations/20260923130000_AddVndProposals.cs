using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Предложения по ВНД от сотрудников главному редактору (см. VndProposal):
    ///
    /// 1. vnd_proposal - само предложение (текст, автор, ВНД/редакция, отметка "прочитано");
    /// 2. vnd_proposal_quote - цитаты из текста редакции ("+ Сослаться на текст редакции");
    /// 3. vnd_proposal_attachment - приложенные файлы;
    /// 4. право 61 ManageVndProposals - получать и разбирать предложения. Добавляется в роли
    ///    "Администратор" (id=1) и "Главный редактор ВНД" (id=4), у которых по сиду "все права".
    ///    Через array_append, а не UpdateData целым массивом: так не затираются права, которые
    ///    администратор мог поменять у этих ролей вручную.
    ///
    /// Миграция написана вручную (без Designer-файла) - как и 20260923110000_AddVndQuoteAnchorsAndRoundAttachments,
    /// атрибуты DbContext/Migration стоят прямо здесь; снапшот модели обновлён соответствующим образом.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260923130000_AddVndProposals")]
    public partial class AddVndProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_proposal",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    redaction_id = table.Column<int>(type: "integer", nullable: true),
                    author_user_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    read_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_proposal", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_vnd_redaction_redaction_id",
                        column: x => x.redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_user_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_user_read_by_user_id",
                        column: x => x.read_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vnd_proposal_quote",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    document_target = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_proposal_quote", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_quote_vnd_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "vnd_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_proposal_attachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    file_attachment_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_proposal_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_attachment_file_attachments_file_attachment_id",
                        column: x => x.file_attachment_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_proposal_attachment_vnd_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "vnd_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_author_user_id",
                table: "vnd_proposal",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_created_at",
                table: "vnd_proposal",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_read_at",
                table: "vnd_proposal",
                column: "read_at");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_read_by_user_id",
                table: "vnd_proposal",
                column: "read_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_redaction_id",
                table: "vnd_proposal",
                column: "redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_vnd_id",
                table: "vnd_proposal",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_attachment_file_attachment_id",
                table: "vnd_proposal_attachment",
                column: "file_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_attachment_proposal_id",
                table: "vnd_proposal_attachment",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_proposal_quote_proposal_id",
                table: "vnd_proposal_quote",
                column: "proposal_id");

            migrationBuilder.Sql(
                "UPDATE role SET permission_codes = array_append(permission_codes, 61) " +
                "WHERE id IN (1, 4) AND NOT (61 = ANY(permission_codes));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE role SET permission_codes = array_remove(permission_codes, 61) WHERE id IN (1, 4);");

            migrationBuilder.DropTable(name: "vnd_proposal_attachment");
            migrationBuilder.DropTable(name: "vnd_proposal_quote");
            migrationBuilder.DropTable(name: "vnd_proposal");
        }
    }
}
