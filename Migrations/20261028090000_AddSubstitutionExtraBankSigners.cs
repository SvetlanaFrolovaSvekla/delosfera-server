using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Дополнительные подписанты договора о материальной ответственности от имени Банка:
    /// substitution_requests.extra_bank_signer_user_ids — кого добавил делопроизводитель в заявке,
    /// substitution_documents.extra_bank_signer_user_ids — зафиксированы с версией документа. Основной
    /// подписант (Председатель Правления) по-прежнему в настройках маршрута УЧР и в director_user_id.
    /// У существующих строк — пустой массив. Миграция написана вручную (без Designer-файла), снапшот
    /// модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261028090000_AddSubstitutionExtraBankSigners")]
    public partial class AddSubstitutionExtraBankSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "extra_bank_signer_user_ids",
                table: "substitution_requests",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.AddColumn<int[]>(
                name: "extra_bank_signer_user_ids",
                table: "substitution_documents",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "extra_bank_signer_user_ids",
                table: "substitution_requests");

            migrationBuilder.DropColumn(
                name: "extra_bank_signer_user_ids",
                table: "substitution_documents");
        }
    }
}
