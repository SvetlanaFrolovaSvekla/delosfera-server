using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceOrgUnitsWithIsribSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Порядок операций ниже намеренно отличается от того, что автогенерирует EF: сначала
            // переименовываем оставшиеся подразделения и вставляем новое (id=9), потом перевешиваем
            // на них ссылки из user/vnd_coordination_default_approver, и только НАКОНЕЦ удаляем старые
            // строки справочника - иначе delete старых id (пока на них ещё ссылаются user/approver)
            // падает по внешнему ключу. Также вырезаны операции над procurement_protocol/
            // procurement_supplier/procurement_request/procurement_contract/meeting_agenda_item/
            // acknowledgement_sheets/sz_proposed_assignee/hr_order - это дубликаты того, что уже
            // применено более ранними миграциями (ContractRulesAndProtocolPerMeeting и др.);
            // они попали в диff из-за рассинхронизированного на момент генерации
            // DelosferaDbContextModelSnapshot.cs и в реальной БД уже существуют.
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "kind", "title_en", "title_kg", "title_ru" },
                values: new object[] { 1, null, null, "Совет директоров" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 1, null, null, null, "Правление" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "curator_user_id", "head_user_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, null, null, "Управление комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Управление продаж малого и среднего бизнеса" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Управление кредитования" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 2, null, null, null, "Управление по работе с виртуальными активами" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 2, null, null, null, "Операционное управление" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Управление информационных технологий" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Управление риск-менеджмента" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Управление методологии" });

            migrationBuilder.InsertData(
                table: "dictionary_organization_unit",
                columns: new[] { "id", "created_at", "curator_user_id", "external_id", "head_user_id", "kind", "parent_id", "requires_paper_sz", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[] { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Юридическое управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56 });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "org_unit_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                column: "org_unit_id",
                value: 9);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                column: "org_unit_id",
                value: 11);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "org_unit_id",
                value: 2);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "org_unit_id",
                value: 8);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                column: "org_unit_id",
                value: 8);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "org_unit_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "org_unit_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "org_unit_id",
                value: 9);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "org_unit_id",
                value: 11);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "org_unit_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                column: "org_unit_id",
                value: 9);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 2,
                column: "org_unit_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 3,
                column: "org_unit_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: 11);

            // Аналогично user/vnd_coordination_default_approver выше: перевешиваем vnd_document.developer_id
            // на подразделения из нового сида ДО удаления старых строк справочника (см. VndDocumentConfiguration.cs).
            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "developer_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "developer_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "developer_id",
                value: 7);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "developer_id",
                value: 9);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "developer_id",
                value: 11);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "kind", "title_en", "title_kg", "title_ru" },
                values: new object[] { 2, "Administrative Affairs Department", "Иштерди башкаруу", "Управление делами" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 0, 1, "Head of Administrative Affairs Department", "ИБ башчысы", "Начальник УД" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "curator_user_id", "head_user_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 14, 8, "Information Technology Department", "Маалыматтык технологиялар башкармасы", "Управление информационных технологий" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Treasury Operations Department", "Казыналык операциялар башкармасы", "Управление казначейских операций" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Compliance Control Department", "Комплаенс контролдоо башкармасы", "Управление комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 3, 5, "Compliance Control Division", "Комплаенс контролдоо бөлүмү", "Отдел комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "kind", "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 3, 5, "Quality Control Division", "Сапатты контролдоо бөлүмү", "Отдел контроля качества" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Lending Department", "Кредиттөө башкармасы", "Управление кредитования" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Marketing Department", "Маркетинг башкармасы", "Управление маркетинга" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Loan Collateral Department", "Кредиттерди камсыздоо башкармасы", "Управление обеспечения кредитов" });

            migrationBuilder.InsertData(
                table: "dictionary_organization_unit",
                columns: new[] { "id", "created_at", "curator_user_id", "external_id", "head_user_id", "kind", "parent_id", "requires_paper_sz", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Loan Collateral Department (LCD)", "Кредиттерди камсыздоо башкармасы (ККБ)", "Управление обеспечения кредитов (УОК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Payment Services Department", "Төлөм сервистери башкармасы", "Управление платежных сервисов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Customer Relations Department", "Кардарлар менен мамилелер башкармасы", "Управление по взаимоотношениям с клиентами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Problem Loans Department", "Көйгөйлүү кредиттер менен иштөө башкармасы", "Управление по работе с проблемными кредитами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Other Property Management Department", "Башка мүлк менен иштөө башкармасы", "Управление по работе с прочей собственностью", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Business Support Department", "Бизнести колдоо башкармасы", "Управление поддержки бизнеса", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Sales Department", "Сатуулар башкармасы", "Управление продаж", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "SME Sales Department", "ЧОБ сатуулар башкармасы", "Управление продаж МСБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Risk Management Department", "Тобокелдиктерди башкаруу башкармасы", "Управление рисками", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Risk Management Office", "Тобокелдик-менеджмент башкармасы", "Управление риск-менеджмента", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Retail Sales Department", "Чекене сатуулар башкармасы", "Управление розничных продаж", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Retail Products Department", "Чекене продукттар башкармасы", "Управление розничных продуктов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 31, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, null, 7, 2, null, false, "Strategic Planning and Budgeting Department", "Стратегиялык пландоо жана бюджеттөө башкармасы", "Управление стратегического планирования и бюджетирования", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Human Resources Department", "Адам ресурстарын башкаруу", "Управление человеческими ресурсами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 33, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Methodology and Products Department", "Методология жана продукттар башкармасы", "Управление методологии", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 34, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, "Legal Department", "Юридикалык башкарма", "Юридическое управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 35, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, null, 4, 0, null, false, "Administrative Division", "Административдик бөлүм", "Административный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 36, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, null, 5, 1, null, false, "Management Board", "Башкарма", "Правление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 37, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, "Chancellery", "Канцелярия", "Канцелярия", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 38, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, null, 11, 0, null, false, "Accounting and Reporting Department", "ЭБжО башкармасы", "УБУиО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 39, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, "Security Department", "Коопсуздук департаменти", "Департамент безопасности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "dictionary_organization_unit",
                columns: new[] { "id", "created_at", "curator_user_id", "external_id", "head_user_id", "kind", "parent_id", "requires_paper_sz", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 13, false, "Claims Handling Division", "Дооматтык иштер бөлүмү", "Отдел претензионной работы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 13, false, "Payment Systems Accounting Division", "Төлөм системаларын эсепке алуу бөлүмү", "Отдел учета платежных систем", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 13, false, "Acquiring Division", "Эквайринг бөлүмү", "Отдел эквайринга", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 13, false, "Card Issuance Division", "Эмиссия бөлүмү", "Отдел эмиссии", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 22, false, "Operational Support Division", "Операциялык коштоо бөлүмү", "Отдел операционного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 24, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 22, false, "Regional Support Division", "Аймактык коштоо бөлүмү", "Отдел регионального сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 27, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 3, 26, false, "Operational Risk Sector", "ТБ операциялык тобокелдиктер секторуу", "Сектор операционных рисков УР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54 });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "org_unit_id",
                value: 26);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                column: "org_unit_id",
                value: 34);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                column: "org_unit_id",
                value: 33);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: 35);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "org_unit_id",
                value: 36);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                column: "org_unit_id",
                value: 37);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                column: "org_unit_id",
                value: 38);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "org_unit_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                column: "org_unit_id",
                value: 32);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                column: "org_unit_id",
                value: 39);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                column: "org_unit_id",
                value: 38);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                column: "org_unit_id",
                value: 4);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                column: "org_unit_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "org_unit_id",
                value: 28);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "org_unit_id",
                value: 5);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "org_unit_id",
                value: 34);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "org_unit_id",
                value: 33);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "org_unit_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "org_unit_id",
                value: 37);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                column: "org_unit_id",
                value: 34);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 2,
                column: "org_unit_id",
                value: 28);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 3,
                column: "org_unit_id",
                value: 5);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: 33);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "developer_id",
                value: 26);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "developer_id",
                value: 26);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "developer_id",
                value: 38);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "developer_id",
                value: 32);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "developer_id",
                value: 33);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 9);
        }
    }
}
