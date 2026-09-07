using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceOrgUnitsWithRealStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ПОРЯДОК ОПЕРАЦИЙ ИЗМЕНЁН ВРУЧНУЮ (сгенерированный EF порядок ронял FK):
            // сначала обновляем/добавляем реальные подразделения, потом переставляем ссылки
            // (user/vnd_coordination_default_approver/vnd_document) на новые id, и только
            // теперь удаляем старые подразделения (4, 7, 9), на которые никто уже не ссылается.
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 2, "Управление делами" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 0, "Начальник УД" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { 80, "Управление информационных технологий" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { 64, "Управление комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 3, "Отдел комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { 44, "Управление кредитования" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { 62, "Управление маркетинга" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                column: "title_ru",
                value: "Управление обеспечения кредитов");

            migrationBuilder.InsertData(
                table: "dictionary_organization_unit",
                columns: new[] { "id", "created_at", "curator_user_id", "external_id", "head_user_id", "kind", "parent_id", "requires_paper_sz", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 60, null, 2, null, false, null, null, "Управление платежных сервисов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 77, null, 3, null, false, null, null, "Отдел претензионной работы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 75, null, 3, null, false, null, null, "Отдел эквайринга", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 74, null, 3, null, false, null, null, "Отдел эмиссии", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 53, null, 2, null, false, null, null, "Управление по работе с проблемными кредитами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 51, null, 2, null, false, null, null, "Управление по работе с прочей собственностью", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление поддержки бизнеса", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление продаж", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление рисками", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 56, null, 2, null, false, null, null, "Управление риск-менеджмента", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление розничных продаж", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление розничных продуктов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 34, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 39, null, 2, null, false, null, null, "Юридическое управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 35, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 55, null, 3, null, false, null, null, "Административный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 36, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, null, 3, null, false, null, null, "Правление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 37, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, null, null, "Канцелярия", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 38, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, null, null, "УБУиО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 39, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Департамент безопасности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 40, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, null, null, "Отдел устойчивого развития", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 41, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 38, null, 2, null, false, null, null, "Управление внутреннего аудита", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 42, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 48, null, 3, null, false, null, null, "Корпоративный секретарь", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 43, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 58, null, 2, null, false, null, null, "Управление бухгалтерского учета и отчетности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 44, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 59, null, 2, null, false, null, null, "Управление планирования и анализа", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 45, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 57, null, 2, null, false, null, null, "Управление по работе с залоговым обеспечением", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 46, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 45, null, 2, null, false, null, null, "Операционное управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 48, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 52, null, 2, null, false, null, null, "Управление безопасности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 49, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 40, null, 2, null, false, null, null, "Казначейство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 50, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 42, null, 3, null, false, null, null, "Отдел информационной безопасности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 51, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 41, null, 2, null, false, null, null, "Бэк-офис", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 52, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 65, null, 3, null, false, null, null, "Отдел методологии", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 55, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 46, null, 3, null, false, null, null, "Отдел кредитного администрирования", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 56, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 49, null, 3, null, false, null, null, "Сектор закупок", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 57, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 70, null, 3, null, false, null, null, "Сектор делопроизводства", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 58, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, null, null, "Секретарь Правления", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 59, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, null, 2, null, false, null, null, "Керемет Банк Головной офис", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 60, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 78, null, 3, null, false, null, null, "Контакт-центр", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 61, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 100, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 62, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 71, null, 3, null, false, null, null, "Отдел отчетности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 63, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 68, null, 3, null, false, null, null, "Отдел розничного кредитования", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 64, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 81, null, 3, null, false, null, null, "Отдел системного и сетевого сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 65, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 29, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 66, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 102, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 67, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 79, null, 3, null, false, null, null, "Персонал при Руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 68, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 87, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 69, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 70, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 12, null, 3, null, false, null, null, "Сберегательная касса  № 049-13-41 (Нарын)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 71, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 21, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 72, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 111, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 73, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 120, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 74, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 128, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 75, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 66, null, 3, null, false, null, null, "Сектор межбанковских операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 76, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 30, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 77, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 103, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 78, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 69, null, 3, null, false, null, null, "Отдел кредитования и документарных операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 79, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 82, null, 3, null, false, null, null, "Отдел технического сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 80, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 72, null, 3, null, false, null, null, "Отдел учета АУР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 81, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 22, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 82, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 121, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 83, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 129, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 84, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 85, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 112, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 86, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 13, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 87, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 67, null, 3, null, false, null, null, "Сектор платежей и расчетов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 88, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Чолпон-Ата»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 89, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 88, null, 3, null, false, null, null, "Юридический сектор", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 23, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 91, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 122, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 92, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 130, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 93, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 94, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 31, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 95, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 89, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 96, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 83, null, 3, null, false, null, null, "Отдел поддержки и развития АБС", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 97, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 76, null, 3, null, false, null, null, "Отдел учета", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 98, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 14, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 99, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 113, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 73, null, 3, null, false, null, null, "Отдел учета клиентских операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 104, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 11, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Иссык-Куль»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 15, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 104, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 114, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 123, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 106, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 107, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 24, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 32, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 109, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 90, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 84, null, 3, null, false, null, null, "Отдел разработки и интеграции иных программных продуктов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 111, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 131, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 112, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 105, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 113, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 20, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Каракол»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 114, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 106, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 115, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 132, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 116, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 124, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 117, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 85, null, 3, null, false, null, null, "Отдел системного и бизнес анализа", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 118, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 8, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 119, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 16, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 120, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 25, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 121, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 115, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 122, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 33, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 123, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 91, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 124, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 28, null, 2, null, false, null, null, "Филиал ОАО  «Керемет Банк - Кадамжай»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 125, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 9, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 126, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 92, null, 3, null, false, null, null, "Кредитный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 127, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 17, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 128, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 26, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 129, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 116, null, 3, null, false, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 130, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 107, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 131, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 133, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 132, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 34, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 133, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 125, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 134, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 86, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Бишкек»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 135, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 18, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 136, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 117, null, 3, null, false, null, null, "Отдел кассовых операций", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 137, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 43, null, 3, null, false, null, null, "Отдел корреспондентских отношений", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 138, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 93, null, 3, null, false, null, null, "Отдел учета и финансового контроля", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 139, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 126, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 140, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 134, null, 3, null, false, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 141, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 27, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 142, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 108, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 143, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 10, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 144, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 35, null, 3, null, false, null, null, "Сектор кредитного сопровождения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 145, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 99, null, 2, null, false, null, null, "Филиал ОАО  «Керемет Банк -Чуй»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 146, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 19, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 147, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 94, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 148, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 36, null, 3, null, false, null, null, "Сберегательная касса № 049-20-46 (Баткен)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 149, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 118, null, 3, null, false, null, null, "Сберегательная касса № 049-25-36 (Таш-Кумыр)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 150, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 109, null, 3, null, false, null, null, "Сберегательная касса № 049-32-34 (Узген)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 151, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 101, null, 2, null, false, null, null, "Филиал ОАО  «Керемет Банк - Ош»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 152, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 37, null, 3, null, false, null, null, "Сберегательная касса № 049-20-18 (Кызыл-Кия)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 153, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 95, null, 3, null, false, null, null, "Сектор по обслуживанию юридических лиц", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 154, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 110, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Жалалабат»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 155, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 96, null, 3, null, false, null, null, "Сектор по обслуживанию физических лиц", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 156, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 119, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Кара-Балта»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 157, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 47, null, 3, null, false, null, null, "Отдел систем денежных перевод", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 158, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 97, null, 3, null, false, null, null, "Сберегательная касса № 049-01-45 (Манас)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 159, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 127, null, 2, null, false, null, null, "Филиал ОАО «Керемет Банк - Талас»", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 160, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 98, null, 3, null, false, null, null, "Сберегательная касса № 049-01-10 (Токмок)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 161, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 50, null, 3, null, false, null, null, "Отдел Антифрод", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 162, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 54, null, 2, null, false, null, null, "Управление  человеческими ресурсами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 163, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 61, null, 3, null, false, null, null, "Отдел дистанционного банковского обслуживания", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 164, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 63, null, 3, null, false, null, null, "Руководство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 165, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 0, null, false, null, null, "Прогон ролей", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "org_unit_id",
                value: 28);

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
                value: 52);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "org_unit_id",
                value: 36);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "org_unit_id",
                value: 3);

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
                value: 52);

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
                value: 52);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "developer_id",
                value: 28);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "developer_id",
                value: 28);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "developer_id",
                value: 46);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "developer_id",
                value: 34);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "developer_id",
                value: 22);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "developer_id",
                value: 52);

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 4, 5 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 32, 4 });

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4);

            // Вставка (49,5)/(162,4) сюда не попадает: демо-документы vnd_document id 4 и 5
            // в этой локальной базе уже удалены руками, ссылаться не на что.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14);

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
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26);

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
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 162);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 1, "Совет директоров" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 1, "Правление" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { null, "Управление комплаенс контроля" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { null, "Управление кредитования" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "kind", "title_ru" },
                values: new object[] { 2, "Управление по работе с виртуальными активами" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { null, "Управление информационных технологий" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "external_id", "title_ru" },
                values: new object[] { null, "Управление риск-менеджмента" });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                column: "title_ru",
                value: "Управление методологии");

            migrationBuilder.InsertData(
                table: "dictionary_organization_unit",
                columns: new[] { "id", "created_at", "curator_user_id", "external_id", "head_user_id", "kind", "parent_id", "requires_paper_sz", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Управление продаж малого и среднего бизнеса", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Операционное управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, 2, null, false, null, null, "Юридическое управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

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
                keyValue: 5,
                column: "org_unit_id",
                value: 2);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "org_unit_id",
                value: 8);

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
                keyValue: 5,
                column: "developer_id",
                value: 4);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "developer_id",
                value: 11);

            migrationBuilder.InsertData(
                table: "vnd_responsible_executor",
                columns: new[] { "organization_unit_id", "vnd_id" },
                values: new object[,]
                {
                    { 32, 4 },
                    { 4, 5 }
                });
        }
    }
}
