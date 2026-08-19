using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AdminConfigurableTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "definition_id",
                table: "document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "field_values",
                table: "document",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dictionary_custom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_hierarchical = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_custom", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_type_definition",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    route_template_id = table.Column<int>(type: "integer", nullable: true),
                    number_pattern = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_type_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_type_definition_route_templates_route_template_id",
                        column: x => x.route_template_id,
                        principalTable: "route_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "list_view",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scope = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    columns = table.Column<string>(type: "jsonb", nullable: false),
                    filter = table.Column<string>(type: "jsonb", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_list_view", x => x.id);
                    table.ForeignKey(
                        name: "fk_list_view_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_custom_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dictionary_id = table.Column<int>(type: "integer", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "text", nullable: true),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_custom_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_custom_item_dictionary_custom_dictionary_id",
                        column: x => x.dictionary_id,
                        principalTable: "dictionary_custom",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dictionary_custom_item_dictionary_custom_item_parent_id",
                        column: x => x.parent_id,
                        principalTable: "dictionary_custom_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_type_field",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    dictionary_id = table.Column<int>(type: "integer", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    show_in_list = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    hint = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_type_field", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_type_field_dictionary_custom_dictionary_id",
                        column: x => x.dictionary_id,
                        principalTable: "dictionary_custom",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_type_field_document_type_definition_definition_id",
                        column: x => x.definition_id,
                        principalTable: "document_type_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_definition_id",
                table: "document",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_custom_code",
                table: "dictionary_custom",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_custom_item_dictionary_id_order",
                table: "dictionary_custom_item",
                columns: new[] { "dictionary_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_custom_item_parent_id",
                table: "dictionary_custom_item",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_type_definition_code",
                table: "document_type_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_type_definition_route_template_id",
                table: "document_type_definition",
                column: "route_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_type_field_definition_id_code",
                table: "document_type_field",
                columns: new[] { "definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_type_field_dictionary_id",
                table: "document_type_field",
                column: "dictionary_id");

            migrationBuilder.CreateIndex(
                name: "ix_list_view_scope_user_id",
                table: "list_view",
                columns: new[] { "scope", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_list_view_user_id",
                table: "list_view",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_document_document_type_definitions_definition_id",
                table: "document",
                column: "definition_id",
                principalTable: "document_type_definition",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_document_document_type_definitions_definition_id",
                table: "document");

            migrationBuilder.DropTable(
                name: "dictionary_custom_item");

            migrationBuilder.DropTable(
                name: "document_type_field");

            migrationBuilder.DropTable(
                name: "list_view");

            migrationBuilder.DropTable(
                name: "dictionary_custom");

            migrationBuilder.DropTable(
                name: "document_type_definition");

            migrationBuilder.DropIndex(
                name: "ix_document_definition_id",
                table: "document");

            migrationBuilder.DropColumn(
                name: "definition_id",
                table: "document");

            migrationBuilder.DropColumn(
                name: "field_values",
                table: "document");
        }
    }
}
