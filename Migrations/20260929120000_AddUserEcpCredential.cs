using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Сохранённая ЭЦП-идентификация пользователя (ПИН + зашифрованный код из СМС/почты) —
    /// см. UserEcpCredential. Миграция написана вручную (без Designer-файла, как и остальные последние
    /// миграции проекта), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260929120000_AddUserEcpCredential")]
    public partial class AddUserEcpCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_ecp_credential",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    person_idnp = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    encrypted_code = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_ecp_credential", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_ecp_credential_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_user_ecp_credential_user_id",
                table: "user_ecp_credential",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_ecp_credential");
        }
    }
}
