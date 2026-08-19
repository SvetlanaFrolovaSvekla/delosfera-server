using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SimpleSignatureRegulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simple_signature_consent",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simple_signature_consent", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simple_signature_regulation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simple_signature_regulation", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_simple_signature_consent_user_id_version",
                table: "simple_signature_consent",
                columns: new[] { "user_id", "version" },
                unique: true);

            // Регламент нужен системе с первого дня: без действующей редакции никто
            // не сможет ничего подписать. Текст — рабочая редакция для стенда,
            // юридическая служба правит его в дальнейшем без пересборки.
            migrationBuilder.Sql(@"
                insert into simple_signature_regulation (version, title, body, is_active, created_at, updated_at)
                values (
                    '1.0',
                    'Регламент применения простой электронной подписи',
                    'Настоящий регламент определяет порядок применения простой электронной подписи (ПЭП) в системе электронного документооборота ОАО «Керемет Банк».

1. Простая электронная подпись — подтверждение того, что документ подписан именно вами. Подписью считается ваше действие в системе: нажатие кнопки «Согласовать», «Подписать» или «Отклонить» под документом.

2. Личность подтверждается входом в систему по доменной учётной записи. Пароль известен только вам; передавать его другим сотрудникам запрещено.

3. В момент подписания система фиксирует ваши фамилию, имя и должность, дату и время, а также отпечаток подписанной версии документа. Изменение документа после подписания делает расхождение видимым.

4. Стороны признают документы, подписанные простой электронной подписью в системе, равнозначными документам на бумаге, подписанным собственноручно, — в соответствии с Законом Кыргызской Республики «Об электронной подписи».

5. Об утрате контроля над учётной записью (утечка пароля, доступ посторонних) необходимо немедленно сообщить в подразделение информационных технологий и в службу безопасности.

6. Настоящий регламент не распространяется на документы, для которых законодательством или внутренними документами Банка требуется квалифицированная электронная подпись.',
                    true, now(), now())");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simple_signature_consent");

            migrationBuilder.DropTable(
                name: "simple_signature_regulation");
        }
    }
}
