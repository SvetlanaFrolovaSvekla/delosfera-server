using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Рассылка «Новые редакции ВНД»: настройки (одна запись) и получатели — письмо при
    /// каждой консолидации ВНД. Миграция написана вручную (без Designer-файла), снапшот модели
    /// обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261001120000_AddVndNewRedactionMailing")]
    public partial class AddVndNewRedactionMailing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_new_redaction_mailing_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    notify_in_app = table.Column<bool>(type: "boolean", nullable: false),
                    notify_email = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_new_redaction_mailing_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vnd_new_redaction_mailing_recipient",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_new_redaction_mailing_recipient", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_vnd_new_redaction_mailing_recipient_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_new_redaction_mailing_recipient");
            migrationBuilder.DropTable(name: "vnd_new_redaction_mailing_settings");
        }
    }
}
