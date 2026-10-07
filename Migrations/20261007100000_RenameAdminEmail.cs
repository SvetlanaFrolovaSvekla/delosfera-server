using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>
    /// Переименовывает учётку администратора СЭД: admin@keremetbank.kg → edoadmin@keremetbank.kg.
    /// admin@keremetbank.kg — зарезервированный адрес администратора домена (группа ssa@keremetbank.kg),
    /// уведомления СЭД на него отправлять нельзя. Id, роли, пароль и история документов не меняются —
    /// меняется только e-mail.
    ///
    /// Письма, которые ещё стоят в очереди на старый адрес, помечаются как неотправляемые.
    /// Если новый адрес уже занят — миграция ничего не делает (идемпотентна).
    ///
    /// После выкладки поменяйте Bootstrap:AdminEmail в env/secrets на новый адрес.
    /// Без Designer-файла; модель не меняется, снапшот не трогаем.
    /// </summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261007100000_RenameAdminEmail")]
    public partial class RenameAdminEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    old_email constant text := 'admin@keremetbank.kg';
    new_email constant text := 'edoadmin@keremetbank.kg';
BEGIN
    IF EXISTS (SELECT 1 FROM ""user"" WHERE lower(email) = new_email) THEN
        RAISE WARNING 'RenameAdminEmail: адрес % уже занят — переименование пропущено', new_email;
    ELSE
        UPDATE ""user"" SET email = new_email, updated_at = now() WHERE lower(email) = old_email;
    END IF;

    UPDATE outgoing_email
       SET failed = true, last_error = 'Адрес зарезервирован под администратора домена'
     WHERE lower(to_address) = old_email AND sent_at IS NULL AND failed = false;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Возвращать зарезервированный адрес в СЭД не нужно.
        }
    }
}
