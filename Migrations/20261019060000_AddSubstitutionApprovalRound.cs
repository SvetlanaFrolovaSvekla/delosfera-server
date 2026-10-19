using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Круг согласования этапа заявки на замещение (substitution_approvals.round):
    /// повторная отправка после отклонения больше не удаляет этапы прошлого круга, а заводит
    /// новый круг. Существующие этапы — первый круг. Миграция написана вручную (без
    /// Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261019060000_AddSubstitutionApprovalRound")]
    public partial class AddSubstitutionApprovalRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "round",
                table: "substitution_approvals",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "round",
                table: "substitution_approvals");
        }
    }
}
