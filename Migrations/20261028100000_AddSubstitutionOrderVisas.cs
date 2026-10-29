using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Визирующие приказов о замещении (hr_routing_settings.order_visa_user_ids): постоянный
    /// список для строк «Завизировали:» в приказе, ведётся в настройках маршрута УЧР. У существующей
    /// строки — пустой массив. Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261028100000_AddSubstitutionOrderVisas")]
    public partial class AddSubstitutionOrderVisas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "order_visa_user_ids",
                table: "hr_routing_settings",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_visa_user_ids",
                table: "hr_routing_settings");
        }
    }
}
