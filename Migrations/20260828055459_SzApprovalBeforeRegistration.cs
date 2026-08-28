using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Перезапись role.permission_codes убрана — третий раз подряд. EF вписывает
    /// снимок набора прав, а снимок молча возвращает роли то, что банк осознанно
    /// у неё забрал. Права на новые разделы раздаёт RolePermissionDefaults при
    /// старте: он добавляет недостающее и ничего не отбирает.
    ///
    /// Сама перестройка порядка статусов схемы не меняет: статусы служебной
    /// записки хранятся строкой в document.status_code, и новых столбцов под
    /// «На согласовании» и «На подписании» не нужно.
    /// </remarks>
    public partial class SzApprovalBeforeRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
