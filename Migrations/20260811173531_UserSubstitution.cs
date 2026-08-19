using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class UserSubstitution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_substitution",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    substitute_user_id = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_substitution", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_substitution_users_substitute_user_id",
                        column: x => x.substitute_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_substitution_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_substitution_substitute_user_id_starts_on_ends_on",
                table: "user_substitution",
                columns: new[] { "substitute_user_id", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_user_substitution_user_id_starts_on_ends_on",
                table: "user_substitution",
                columns: new[] { "user_id", "starts_on", "ends_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_substitution");
        }
    }
}
