using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Членство в коллегиальном органе — не право роли. Правило это было, но его
    /// миграция потерялась при слиянии, а синхронизация справочников заново
    /// проставила администратору все права, включая признаки членства. Возвращаем
    /// правило на уровне данных: у ролей, кроме самих органов, признаки членства
    /// снимаются; членство ведётся справочником состава органа.
    ///
    /// 32/33/34 — MemberOfBoard / MemberOfKpa / MemberOfCreditCommittee.
    /// </summary>
    public partial class RestoreBoardMembershipRule : Migration
    {
        private const string Flags = "ARRAY[32, 33, 34]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Роли самих органов признак сохраняют — они для того и заведены.
            migrationBuilder.Sql($@"
                UPDATE role
                SET permission_codes = ARRAY(
                        SELECT code FROM unnest(permission_codes) AS code
                        WHERE code <> ALL ({Flags})
                    )
                WHERE permission_codes && {Flags}
                  AND title_ru !~* 'Правлени|КПА|Кредитн комитет|Кредитный комитет';
            ");

            // Правление и председатель признак получают: у ролей, заведённых
            // отдельно от сида, его не было вовсе.
            migrationBuilder.Sql(@"
                UPDATE role
                SET permission_codes = permission_codes || 32
                WHERE title_ru ~* 'Правлени'
                  AND NOT (32 = ANY(permission_codes));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат вернул бы признак членства администратору — правило создано
            // ровно чтобы этого не было, поэтому Down ничего не восстанавливает.
        }
    }
}
