using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowTaskDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "document_id",
                table: "workflow_task",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_entity_id",
                table: "workflow_task",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_id",
                table: "workflow_task");

            migrationBuilder.DropColumn(
                name: "source_entity_id",
                table: "workflow_task");
        }
    }
}
