using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>
    /// Членство в коллегиальном органе перестаёт быть правом доступа.
    ///
    /// Роли администратора и главного редактора ВНД заводились с полным перечнем
    /// прав, а в перечне лежали признаки состава органов. Оттого членами Правления
    /// числились айтишники и методологи: в поле «Кому» служебной записки на
    /// Правление предлагались они, а настоящих членов — председателя, зампредов,
    /// члена Правления-главного бухгалтера — там не было.
    ///
    /// Признаки снимаются точечно, а не перезаписью всего набора прав: права ролей
    /// на стенде правились и вручную, и их незачем возвращать к сидовым.
    /// </summary>
    public partial class BoardMembershipNotAPermission : Migration
    {
        /// <summary>Признаки членства: Правление (32), КПА (33), Кредитный комитет (34).</summary>
        private const string Flags = "ARRAY[32, 33, 34]";

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Возврат признака администратору и главному редактору ВНД: их роли
            // заводились полным перечнем прав.
            migrationBuilder.Sql($@"
                UPDATE role
                SET permission_codes = permission_codes || {Flags}
                WHERE id IN (1, 4) AND NOT (permission_codes && {Flags});
            ");
        }
    }
}
