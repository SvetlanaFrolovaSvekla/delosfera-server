using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceKeywordRubricWithIsrib : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vnd_keyword",
                keyColumns: new[] { "keyword_id", "vnd_id" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "vnd_keyword",
                keyColumns: new[] { "keyword_id", "vnd_id" },
                keyValues: new object[] { 6, 4 });

            migrationBuilder.DeleteData(
                table: "vnd_keyword",
                keyColumns: new[] { "keyword_id", "vnd_id" },
                keyValues: new object[] { 8, 3 });

            migrationBuilder.DeleteData(
                table: "vnd_keyword",
                keyColumns: new[] { "keyword_id", "vnd_id" },
                keyValues: new object[] { 11, 2 });

            migrationBuilder.DeleteData(
                table: "vnd_keyword",
                keyColumns: new[] { "keyword_id", "vnd_id" },
                keyValues: new object[] { 12, 1 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 8, 2 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 26, 1 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 26, 2 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 38, 3 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 49, 5 });

            migrationBuilder.DeleteData(
                table: "vnd_responsible_executor",
                keyColumns: new[] { "organization_unit_id", "vnd_id" },
                keyValues: new object[] { 162, 4 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 5, 1 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 7, 4 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 11, 3 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 11, 5 });

            migrationBuilder.DeleteData(
                table: "vnd_rubric",
                keyColumns: new[] { "rubric_id", "vnd_id" },
                keyValues: new object[] { 15, 3 });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Продукт" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 12, null, null, "Возмещение расходов" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, null, "Сверка" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 5, null, null, "Ежемесячная сверка" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 5, null, null, "Платеж" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Ответственность" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 17, null, null, "Планирование" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 17, null, null, "Финансовое планирование" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Бюджет" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Расход" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Безопасность" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, null, "Матрица" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 3, null, null, "Карточный продукт" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "План" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Формирование" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Резервный сервер" });

            migrationBuilder.InsertData(
                table: "dictionary_keyword",
                columns: new[] { "id", "created_at", "parent_id", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Автоматизация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Непрерывность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Малоценные и быстроизнашивающиеся предметы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 24, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Корпоративный кредит", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Резерв", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 27, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "ГКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Ликвидность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Затраты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Аккредитив", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 31, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Расследование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Тарифы на корпоративное кредитование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 33, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Рамочное соглашение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 34, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "кредиттик линия", "Кредитная линия", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 35, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Индивидуальный договор", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 36, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Управление корпоративного кредитования (УКК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 37, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Лизинг", "Лизинг", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 38, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Доступ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 39, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Улгулуу Келишим", "Типовой договор", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 41, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Корпоративное кредитование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 42, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Залог", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 43, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Платежные карты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 44, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Кредитный лимит", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 45, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Тарифтер", "Тарифы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 46, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Овердрафт", "Овердрафт", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 47, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Корреспондентский счет", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 48, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Ценообразование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 49, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "POS терминал", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 50, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Режим", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 51, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Расчеты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 52, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Дисциплинарный проступок", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 53, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "бизнес процесс", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 54, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Кассир", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 55, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Перевод", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 56, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Денежные переводы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 57, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Документ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 58, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Инкассация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 59, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Операционный день", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 60, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Техподдержка", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 61, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Инсайдер", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 62, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Заявка", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 63, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Кредит", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 64, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Дебит", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 65, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Информационная безопасность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 66, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Риски", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 67, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Кассовые операции", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 68, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Аутсорсинг", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 70, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Оформление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 71, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Программное обеспечение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 72, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Контрольный журнал", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 73, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Заемщик", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 74, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Смета", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 76, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Сберегательная касса", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 77, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Торгово-сервисное предприятие (ТСП)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 78, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Банкомат", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 79, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "ПФТ ОД", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 80, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "ИТ-услуга", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 81, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Лимиты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 82, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Иностранная валюта", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 83, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Хранение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 84, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Компенсация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 85, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Банковская гарантия", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 86, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Подозрительная операция", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 87, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Оценка", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 88, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Антикризисное управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 89, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Платежный терминал", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Резервный центр", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 91, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Верификация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 92, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Конвертация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 93, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Имущество", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 94, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Устройство защитного обеспечения (УЗО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 95, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Авансовый отчет", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 96, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Дисциплинарное взыскание", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 98, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Телефон", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Фильтры", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Счета", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Норматив", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 104, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Инструкция", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Банковская информация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 106, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Начальник", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 107, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Приобретение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Договор", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 109, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Операционный отдел", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Корреспондентские отношения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 111, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "ГКВ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 112, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "основные средства", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 113, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Платежное поручение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 114, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Остатки", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 115, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Должностная инструкция", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 116, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Нематериальные активы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 117, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Каталог", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 118, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Информация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            // ⚠ 07.09.2026: перенесено сюда вручную (изначально EF сгенерировал этот UpdateData
            // раньше InsertData выше) — id=15 ссылается на новый parent_id=21 ("Непрерывность"),
            // а строка с id=21 создаётся только что выполненным InsertData; если оставить как было,
            // нарушается FK dictionary_keyword.parent_id -> dictionary_keyword.id, так как строки
            // с id=21 ещё не существует в момент выполнения UpdateData.
            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 21, null, null, "Непрерывная деятельность" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Классификатор разделов" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Устав" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Организационная структура" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { null, null, "Органы управления" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 4, null, null, "Общее собрание акционеров" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 4, null, null, "Совет директоров банка" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Положение о Совете Директоров" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по назначениям и вознаграждениям" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по управлению рисками" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по аудиту" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по комплаенс-контролю" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по управлению активами и пассивами" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Кредитный комитет" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 6, null, null, "Комитет по проблемным активам" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 4, null, null, "Правление" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 15, null, null, "Председатель Правления" });

            migrationBuilder.InsertData(
                table: "dictionary_rubric",
                columns: new[] { "id", "created_at", "parent_id", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 15, null, null, "Заместитель Председателя Правления", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 15, null, null, "Персонал при руководстве", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 15, null, null, "Положение о Правлении", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 24, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Структурные подразделения", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 156, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Договор (шаблоны, формы)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 157, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 158, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв7", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 159, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв8", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 160, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв9", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 161, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв10", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 162, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв20", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 163, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв30", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 164, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв40", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 165, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв50", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 166, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв60", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 167, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв70", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 168, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв80", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 169, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "резерв90", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 170, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Организационная структура", "Организационная структура", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 173, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Безопасность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 174, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Информационные технологии", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 175, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Управление рисками", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 176, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Внутренний аудит", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 177, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Документарные операции", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 178, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Документооборот и делопроизводство", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 179, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Управление персоналом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 180, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Устав и корпоративное управление", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 181, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Прочее", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 182, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Кредитная деятельность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 190, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Депозиты и расчетно-кассовое обслуживание", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 196, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Платежные карты и БСО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 197, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Интернетбанкинг и платежные системы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 198, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Бухгалтерский учет и отчетность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 199, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Маркетинг и PR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "dictionary_keyword",
                columns: new[] { "id", "created_at", "parent_id", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 23, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 73, null, null, "Корпоративный заемщик", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 50, null, null, "Внутриобъектовый режим", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 40, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 66, null, null, "Риск концентрации", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 69, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42, null, null, "Залоговая стоимость", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 75, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 61, null, null, "Список инсайдеров", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 97, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 50, null, null, "Пропускной режим", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 99, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 73, null, null, "Совокупная задолженность", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42, null, null, "Залогодатель", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "dictionary_rubric",
                columns: new[] { "id", "created_at", "parent_id", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 18, null, null, "Секретарь Правления", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 18, null, null, "Корпоративный секретарь", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 18, null, null, "Советник Председателя Правления", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 18, null, null, "Исполнительный директор", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Корпоративный секретарь", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление внутреннего аудита (УВА)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление комплаенс-контроля (УКК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 36, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление риск-менеджмента (УРМ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 39, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление по работе с проблемными кредитами (УРПК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 40, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление по работе с прочей собственностью (УРПС)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 41, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление безопасности (УБ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 42, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление человеческими ресурсами (УЧР)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 52, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление по работе с залоговым обеспечением (УРЗО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 53, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление маркетинга (УМ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 55, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Операционное управление (ОУ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 57, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление по работе с виртуальными активами (УРВА)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 59, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Юридическое управление (ЮУ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 60, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление бухгалтерского учета и отчетности (УБиО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 61, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление планирования и анализа (УПА)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 63, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление информационных технологий (УИТ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 65, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление платежных сервисов (УПС)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 67, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Управление кредитования (УК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 68, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Казначейство (КО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 76, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Бэк-офис (БО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 93, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел информационной безопасности (ОИБ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 98, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел методологии (ОМ)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел кредитного администрирования (ОКА)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 107, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел дистанционного банковского обслуживания (ОДБО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 109, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Административный отдел (АО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 133, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Сектор закупок (СК)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 139, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел систем денежных переводов (ОСДП)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 146, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел корреспонденских отношений (ОКО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 154, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 24, null, null, "Отдел антифрода (ОА)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 171, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 170, null, "Структурные подразделения Головного офиса", "Структурные подразделения Головного офиса", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 172, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 170, null, "Структурные подразделения филиалов", "Структурные подразделения филиалов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 183, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Кредитование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 184, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Корпоративное кредитование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 185, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Кредитование МСБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 186, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Розничное кредитование", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 187, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Залоговое обеспечение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 188, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Работа с проблемными кредитами", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 189, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 182, null, null, "Админитрирование и верификация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 191, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 190, null, null, "Срочные депозиты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 192, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 190, null, null, "Кассовые операции", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 193, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 190, null, null, "Денежные переводы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 194, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 190, null, null, "Расчетные счета", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 195, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 190, null, null, "Инкассация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 27, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 26, null, null, "Внутренние нормативные документы УВА", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 28, null, null, "Внутренние нормативные документы УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 28, null, null, "Должностные инструкции УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 35, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 28, null, null, "Положение об УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 37, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 36, null, null, "Отдел устойчивого развития (ОУР)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 38, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 36, null, null, "Внутренние нормативные документы УРМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 43, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42, null, null, "Внутренние нормативные документы УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 44, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42, null, null, "Должностные инструкции УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 51, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 42, null, null, "Положение об УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 54, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 53, null, null, "Внутренние нормативные документы УМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 56, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 55, null, null, "Внутренние нормативные документы ОУ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 58, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 57, null, null, "Внутренние нормативные документы УРВА", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 62, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 61, null, null, "Внутренние нормативные документы УПА", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 64, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 63, null, null, "Внутренние нормативные документы УИТ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 66, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 65, null, null, "Внутренние нормативные документы УПС", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 69, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 68, null, null, "Внутренние нормативные документы КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 70, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 68, null, null, "Должностные инструкции КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 75, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 68, null, null, "Положение о Казначействе", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 77, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 76, null, null, "Внутренние нормативные документы БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 78, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 76, null, null, "Положение о БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 81, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 76, null, null, "Сектор межбанковских операций (СМО) БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 87, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 76, null, null, "Сектор платежей и расчетов (СПР) БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 94, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 93, null, null, "Внутренние нормативные документы ОИБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 95, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 93, null, null, "Должностные инструкции ОИБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 97, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 93, null, null, "Положение об ОИБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 99, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 98, null, null, "Должностные инструкции ОМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 98, null, null, "Внутренние нормативные документы ОМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 104, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 98, null, null, "Положение об ОМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 106, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 105, null, null, "Внутренние нормативные документы ОКА", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 107, null, null, "Внутренние нормативные документы ОДБО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 109, null, null, "Сектор делопроизводства (СД АО)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 121, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 109, null, null, "Внутренние нормативные документы АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 122, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 109, null, null, "Должностные инструкции АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 132, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 109, null, null, "Положение об АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 134, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 133, null, null, "Внутренние нормативные документы СК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 135, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 133, null, null, "Должностные инструкции СК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 138, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 133, null, null, "Положение о СК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 140, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 139, null, null, "Внутренние нормативные документы ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 141, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 139, null, null, "Должностные инструкции ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 145, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 139, null, null, "Положение об ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 147, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 146, null, null, "Внутренние нормативные документы ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 148, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 146, null, null, "Должностные инструкции ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 153, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 146, null, null, "Положение об ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 155, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 154, null, null, "Внутренние нормативные документы ОА", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 31, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, null, null, "Начальник УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, null, null, "Заместитель начальника УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 33, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, null, null, "Главный специалист УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 34, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, null, null, "Ведущий специалист УКК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 45, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Начальник УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 46, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Руководитель по обучению и развитию персонала УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 47, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Менеджер по развитию и обучению персонала УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 48, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Главный специалист по кадровой работе УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 49, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Ведущий специалист по кадровой работе УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 50, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 44, null, null, "Заместитель начальника управления-руководителя по кадровой работе УЧР", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 71, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 70, null, null, "Начальник КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 72, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 70, null, null, "Заместитель начальника КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 73, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 70, null, null, "Главный дилер КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 74, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 70, null, null, "Старший дилер КО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 79, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 78, null, null, "Должностные инструкции БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 82, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 81, null, null, "Положение о СМО БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 83, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 81, null, null, "Должностные инструкции СМО БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 88, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 87, null, null, "Положение о СПР БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 89, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 87, null, null, "Должностные инструкции СПР БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 96, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 95, null, null, "Специалист ИБ ОИБ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 99, null, null, "Начальник ОМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 99, null, null, "Главный специалист ОМ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 99, null, "Ведущий методолог_кыргяз", "Ведущий методолог", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 111, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 110, null, null, "Внутренние нормативные документы СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 112, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 110, null, null, "Должностные инструкции СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 120, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 110, null, null, "Положение об СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 123, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Начальник АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 124, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Заместитель начальника АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 125, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Ведущий специалист АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 126, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Специалист по техническим вопросам АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 127, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Энергетик АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 128, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Водитель-курьер АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 129, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Консультант-инженер по тех.вопросам АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 130, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Главный инженер АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 131, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 122, null, null, "Разнорабочий АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 136, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 135, null, null, "Заведующий СК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 137, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 135, null, null, "Ведущий специалист (Зав. складом по оргтехнике) СК", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 142, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 141, null, null, "Начальник ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 143, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 141, null, null, "Главный специалист ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 144, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 141, null, null, "Специалист ОСДП", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 149, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 148, null, null, "Начальник ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 150, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 148, null, null, "Главыный специалист ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 151, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 148, null, null, "Ведущий специалист ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 152, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 148, null, null, "Специалист ОКО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 80, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 79, null, null, "Начальник БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 84, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 83, null, null, "Ведущий специалист СМО БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 85, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 83, null, null, "Заведующий СМО БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 86, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 83, null, null, "Главный специалист СМО БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 90, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 89, null, null, "Заведующий СПР БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 91, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 89, null, null, "Главный специалист СПР БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 92, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 89, null, null, "Ведущий специалист СПР БО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 113, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Заведующий СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 114, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Ведущий специалист СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 115, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Специалист СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 116, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Ассистент руководителя СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 117, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Архивариус СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 118, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Главный специалист  по кыргызскому языку СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 119, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 112, null, null, "Ведущий специалист по кыргызскому языку СД АО", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 171);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 173);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 174);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 175);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 176);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 177);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 178);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 179);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 180);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 181);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 183);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 184);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 185);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 186);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 187);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 188);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 189);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 191);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 192);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 193);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 194);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 195);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 196);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 197);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 198);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 199);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 182);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 190);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Identification", "Идентификациялоо" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Analysis", "Анализ" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Reconciliation", "Салыштыруу", "Сверка" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 3, "Balance Reconciliation", "Калдыктарды салыштыруу", "Сверка остатков" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 3, "Data Reconciliation", "Маалыматтарды салыштыруу", "Сверка данных" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Responsibility", "Жоопкерчилик", "Ответственность" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Budget", "Бюджет", "Бюджет" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Expense", "Чыгым", "Расход" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 8, "Material Expense", "Материалдык чыгым", "Расход материальный" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 8, "Operational Expense", "Операциялык чыгым", "Расход операционный" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Security", "Коопсуздук", "Безопасность" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Matrix", "Матрица", "Матрица" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Plan", "План", "План" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 13, "Schedule", "План-график", "План-график" });

            // ⚠ 07.09.2026: перенесено сюда вручную (изначально EF сгенерировал этот UpdateData
            // раньше DeleteData(id=21) выше) — пока id=15 ссылается на parent_id=21, нельзя удалять
            // строку с id=21 (см. зеркальный комментарий в Up()); сначала возвращаем id=15 обратно
            // на parent_id=13, и только потом удаляем 21 — поэтому и revert, и сам DeleteData(21)
            // перенесены сюда, вместе, в правильном порядке.
            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 13, "Action Plan", "Иш-чаралар планы", "План мероприятий" });

            migrationBuilder.DeleteData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Formation", "Түзүү", "Формирование" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Backup Server", "Резервдик сервер", "Резервный сервер" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Automation", "Автоматташтыруу", "Автоматизация" });

            migrationBuilder.UpdateData(
                table: "dictionary_keyword",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Continuity", "Үзгүлтүксүздүк", "Непрерывность" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Head Office Structural Units", "Башкы офистин түзүмдүк бөлүмдөрү", "Структурные подразделения Головного офиса" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Branch Structural Units", "Филиалдардын түзүмдүк бөлүмдөрү", "Структурные подразделения филиалов" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Security", "Коопсуздук", "Безопасность" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg", "title_ru" },
                values: new object[] { "Information Technology", "Маалыматтык технологиялар", "Информационные технологии" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Risk Management", "Тобокелдиктерди башкаруу", "Управление рисками" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Document Management and Records Keeping", "Документ жүгүртүү жана иш кагаздарын жүргүзүү", "Документооборот и делопроизводство" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Human Resources Management", "Кадрларды башкаруу", "Управление персоналом" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Lending Activity", "Кредиттик иш-аракет", "Кредитная деятельность" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 8, "Retail Lending", "Чекене кредиттөө", "Розничное кредитование" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 8, "Corporate Lending", "Корпоративдик кредиттөө", "Корпоративное кредитование" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Deposits and Cash Settlement Services", "Депозиттер жана эсептешүү-кассалык тейлөө", "Депозиты и расчетно-кассовое обслуживание" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 11, "Deposit Operations", "Депозиттик операциялар", "Депозитные операции" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { 11, "Cash Settlement Services", "Эсептешүү-кассалык тейлөө", "Расчетно-кассовое обслуживание" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Payment Cards and Strict Reporting Forms", "Төлөм карталары жана катуу отчеттуулук бланктары", "Платежные карты и БСО" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Accounting and Reporting", "Эсепке алуу жана отчеттуулук", "Бухгалтерский учет и отчетность" });

            migrationBuilder.UpdateData(
                table: "dictionary_rubric",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "parent_id", "title_en", "title_kg", "title_ru" },
                values: new object[] { null, "Marketing and PR", "Маркетинг жана PR", "Маркетинг и PR" });

            migrationBuilder.InsertData(
                table: "vnd_keyword",
                columns: new[] { "keyword_id", "vnd_id" },
                values: new object[,]
                {
                    { 6, 1 },
                    { 6, 4 },
                    { 8, 3 },
                    { 11, 2 },
                    { 12, 1 }
                });

            migrationBuilder.InsertData(
                table: "vnd_responsible_executor",
                columns: new[] { "organization_unit_id", "vnd_id" },
                values: new object[,]
                {
                    { 8, 2 },
                    { 26, 1 },
                    { 26, 2 },
                    { 38, 3 },
                    { 49, 5 },
                    { 162, 4 }
                });

            migrationBuilder.InsertData(
                table: "vnd_rubric",
                columns: new[] { "rubric_id", "vnd_id" },
                values: new object[,]
                {
                    { 5, 1 },
                    { 5, 2 },
                    { 7, 4 },
                    { 11, 3 },
                    { 11, 5 },
                    { 15, 3 }
                });
        }
    }
}
