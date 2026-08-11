using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>
    /// Обнуляет пароли сид-аккаунтов. Ранее миграции SeedUsersAndRolesInConfig и
    /// UpdateApprovalTestUserPasswords записывали в БД PBKDF2-хеши известных паролей
    /// реальных сотрудников (@keremetbank.kg) — эти хеши лежат в git, то есть пароли
    /// фактически публичны. Здесь хеши заменяются на непроверяемый плейсхолдер, так что
    /// войти по старым паролям нельзя. Доступ администратора выдаётся отдельно —
    /// через bootstrap из конфигурации (Bootstrap:AdminEmail / Bootstrap:AdminPassword),
    /// см. Program.cs.
    /// </summary>
    public partial class InvalidateSeededPasswords : Migration
    {
        // id сид-аккаунтов из SeedUsersAndRolesInConfig (1..13) и тестовых согласующих (14..19)
        private static readonly int[] SeededUserIds =
            { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var id in SeededUserIds)
            {
                migrationBuilder.UpdateData(
                    table: "user",
                    keyColumn: "id",
                    keyValue: id,
                    column: "password_hash",
                    value: "!invalidated!");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат невозможен: исходные хеши намеренно не восстанавливаются
            // (их публичность и была проблемой). Пароли задаются заново через bootstrap/сброс.
        }
    }
}
