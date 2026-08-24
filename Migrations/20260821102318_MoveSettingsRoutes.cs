using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>
    /// Настройки переехали под «Управление»: /users → /management/users и так далее.
    ///
    /// Статьи инструкции привязаны к экранам полем route_path — по нему у страницы
    /// появляется кнопка справки. Заведённые статьи сидер не перезаписывает
    /// (инструкция принадлежит банку, а не сборке), поэтому адреса в них правит
    /// эта миграция. Иначе кнопка справки на переехавших экранах молча перестала бы
    /// появляться, а искать причину пришлось бы в базе.
    ///
    /// Правится и журнал посещений: без этого разделы «Сотрудники» и «Справочники»
    /// разъехались бы на две записи каждый — старую и новую, — и отчёт показал бы
    /// их посещаемость вдвое меньшей, чем она есть.
    /// </summary>
    public partial class MoveSettingsRoutes : Migration
    {
        /// <summary>Что куда переехало. Порядок не важен — адреса не пересекаются.</summary>
        private static readonly (string From, string To)[] Moves =
        [
            ("/users", "/management/users"),
            ("/roles", "/management/roles"),
            ("/substitutions", "/management/substitutions"),
            ("/refs", "/management/refs"),
            ("/audit-log", "/management/audit"),
            ("/usage", "/management/usage"),
            ("/feedback", "/management/feedback"),
            ("/system/settings", "/management/integrations"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (from, to) in Moves)
            {
                // Точное совпадение и вложенные адреса: «/refs» и «/refs/position»
                // переезжают вместе, но «/refsomething» — не наш случай.
                migrationBuilder.Sql($"""
                    UPDATE help_article
                       SET route_path = '{to}' || substring(route_path from {from.Length + 1})
                     WHERE route_path = '{from}' OR route_path LIKE '{from}/%';
                    """);

                migrationBuilder.Sql($"""
                    UPDATE page_visit
                       SET route_path = '{to}' || substring(route_path from {from.Length + 1})
                     WHERE route_path = '{from}' OR route_path LIKE '{from}/%';
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (from, to) in Moves)
            {
                migrationBuilder.Sql($"""
                    UPDATE help_article
                       SET route_path = '{from}' || substring(route_path from {to.Length + 1})
                     WHERE route_path = '{to}' OR route_path LIKE '{to}/%';
                    """);

                migrationBuilder.Sql($"""
                    UPDATE page_visit
                       SET route_path = '{from}' || substring(route_path from {to.Length + 1})
                     WHERE route_path = '{to}' OR route_path LIKE '{to}/%';
                    """);
            }
        }
    }
}
