using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>
    /// Убирает тестовые локальные учётные записи — все с source = 'Local', кроме
    /// администратора (admin@keremetbank.kg). Сюда попадают сид-аккаунты 1–12 и 14–19,
    /// демо-учётки @test.local и всё, что заводили вручную. Учётки из AD (source = 'Ldap'
    /// или с ldap_object_id) не трогаются.
    ///
    /// Удалять всех подряд нельзя: на тестовых пользователей ссылаются документы,
    /// согласования, заседания, аудит (десятки внешних ключей с Restrict и колонки
    /// без ключа). Поэтому:
    ///  • учётка, на которую нигде ничего не ссылается (кроме личного мусора — сессий,
    ///    уведомлений, избранного, сохранённых поисков), удаляется полностью;
    ///  • учётка, оставившая след, блокируется: is_active = false, пароль обнулён,
    ///    сессии, роли и группы сняты. История документов при этом не ломается.
    ///
    /// Перед этим с тестовых снимаются «настроечные» ссылки, которые иначе направляли бы
    /// маршруты на мёртвую учётку: руководитель/куратор подразделения и личный руководитель.
    ///
    /// Что было сделано — видно в таблице zz_removed_test_user (снимок учётки без хеша
    /// пароля, её роли и группы, действие deleted/blocked). Миграция идемпотентна:
    /// повторный прогон добьёт то, что появилось снова (например, при включённом Demo:Enabled).
    ///
    /// Без Designer-файла, как и остальные последние миграции; модель не меняется,
    /// снапшот не трогаем.
    /// </summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260930120000_RemoveLocalTestUsers")]
    public partial class RemoveLocalTestUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    admin_email     constant text   := 'admin@keremetbank.kg';
    -- Личные данные учётки: их можно снести вместе с ней, это не история документов.
    personal_tables constant text[] := ARRAY[
        'token', 'user_role', 'user_group_member', 'user_notification', 'page_visit',
        'saved_search', 'list_view', 'vnd_favorite', 'user_ecp_credential', 'saved_filters',
        'zz_removed_test_user'];
    admin_id    int;
    cand        int[];
    referenced  int[] := '{}';
    hits        int[];
    to_delete   int[];
    to_block    int[];
    col         record;
BEGIN
    SELECT id INTO admin_id FROM ""user"" WHERE lower(email) = admin_email;
    IF admin_id IS NULL THEN
        RAISE WARNING 'RemoveLocalTestUsers: учётки % нет — чистка пропущена, чтобы не остаться без администратора', admin_email;
        RETURN;
    END IF;

    SELECT coalesce(array_agg(id ORDER BY id), '{}') INTO cand
    FROM ""user""
    WHERE source = 'Local'
      AND ldap_object_id IS NULL
      AND id <> admin_id
      -- уже выведенные прошлым прогоном не трогаем повторно
      AND NOT (is_active = false AND block_reason = 'Тестовая учётная запись выведена из эксплуатации');

    IF cardinality(cand) = 0 THEN
        RAISE NOTICE 'RemoveLocalTestUsers: локальных тестовых учёток нет';
        RETURN;
    END IF;

    -- 1. Настроечные ссылки: не история, а указатель «кому слать». Снимаем.
    UPDATE dictionary_organization_unit SET head_user_id = NULL    WHERE head_user_id    = ANY(cand);
    UPDATE dictionary_organization_unit SET curator_user_id = NULL WHERE curator_user_id = ANY(cand);
    UPDATE ""user"" SET manager_id = NULL         WHERE manager_id         = ANY(cand);
    UPDATE ""user"" SET blocked_by_user_id = NULL WHERE blocked_by_user_id = ANY(cand) AND NOT (id = ANY(cand));

    -- 2. Кто оставил след. Смотрим все внешние ключи на ""user"" и, на всякий случай,
    --    целочисленные колонки с «пользовательским» именем без ключа (audit_entry.user_id,
    --    workflow_task.assignee_user_id, signature.user_id и т.п.). Ложное срабатывание
    --    безопасно — учётка просто будет заблокирована, а не удалена.
    FOR col IN
        SELECT DISTINCT x.table_name, x.column_name
        FROM (
            SELECT cl.relname::text AS table_name, a.attname::text AS column_name
            FROM pg_constraint k
            JOIN pg_class cl     ON cl.oid = k.conrelid
            JOIN pg_attribute a  ON a.attrelid = k.conrelid AND a.attnum = ANY(k.conkey)
            WHERE k.contype = 'f' AND k.confrelid = '""user""'::regclass
            UNION
            SELECT c.table_name::text, c.column_name::text
            FROM information_schema.columns c
            JOIN information_schema.tables t
              ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
            WHERE c.table_schema = current_schema()
              AND c.data_type IN ('integer', 'bigint')
              AND c.column_name ~ '(user_id|_by_id|author_id|actor_id|assignee_id|signer_id|approver_id)$'
        ) x
        WHERE x.table_name <> 'user'
          AND NOT (x.table_name = ANY(personal_tables))
    LOOP
        EXECUTE format('SELECT array_agg(DISTINCT %1$I)::int[] FROM %2$I WHERE %1$I = ANY($1)',
                       col.column_name, col.table_name)
            INTO hits USING cand;
        IF hits IS NOT NULL THEN
            referenced := referenced || hits;
        END IF;
    END LOOP;

    to_block  := ARRAY(SELECT unnest(cand) INTERSECT SELECT unnest(referenced) ORDER BY 1);
    to_delete := ARRAY(SELECT unnest(cand) EXCEPT    SELECT unnest(referenced) ORDER BY 1);

    -- 3. Журнал того, что сделали (без хеша пароля).
    CREATE TABLE IF NOT EXISTS zz_removed_test_user (
        uid        int         NOT NULL,
        action     text        NOT NULL,
        snapshot   jsonb       NOT NULL,
        roles      int[],
        groups     int[],
        removed_at timestamptz NOT NULL DEFAULT now()
    );

    INSERT INTO zz_removed_test_user (uid, action, snapshot, roles, groups)
    SELECT u.id,
           CASE WHEN u.id = ANY(to_delete) THEN 'deleted' ELSE 'blocked' END,
           to_jsonb(u) - 'password_hash',
           (SELECT array_agg(r.roles_id)      FROM user_role r         WHERE r.users_id = u.id),
           (SELECT array_agg(g.user_group_id) FROM user_group_member g WHERE g.users_id = u.id)
    FROM ""user"" u
    WHERE u.id = ANY(cand);

    -- 4. Блокировка тех, на ком висит история.
    UPDATE ""user""
    SET is_active             = false,
        blocked_at            = coalesce(blocked_at, now()),
        blocked_by_user_id    = admin_id,
        block_reason          = 'Тестовая учётная запись выведена из эксплуатации',
        password_hash         = '!invalidated!',
        failed_login_attempts = 0,
        locked_until          = NULL,
        updated_at            = now()
    WHERE id = ANY(to_block);

    -- 5. Личные данные — и у блокируемых (чтобы выпали из ролей/групп и сессий), и у удаляемых.
    FOR col IN
        SELECT c.table_name::text AS table_name, c.column_name::text AS column_name
        FROM information_schema.columns c
        WHERE c.table_schema = current_schema()
          AND c.table_name = ANY(personal_tables)
          AND c.table_name <> 'zz_removed_test_user'
          AND c.column_name IN ('user_id', 'users_id', 'owner_user_id')
    LOOP
        EXECUTE format('DELETE FROM %I WHERE %I = ANY($1)', col.table_name, col.column_name)
            USING cand;
    END LOOP;

    -- 6. Полное удаление чистых.
    DELETE FROM ""user"" WHERE id = ANY(to_delete);

    RAISE NOTICE 'RemoveLocalTestUsers: удалено % (%), заблокировано % (%)',
        cardinality(to_delete), to_delete, cardinality(to_block), to_block;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат не делаем: тестовые учётки возвращать незачем, а пароли у них
            // обнулены. Что было удалено/заблокировано — в таблице zz_removed_test_user.
        }
    }
}
