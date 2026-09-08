using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("dictionary_keyword");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ⚠ 07.09.2026: полная замена — 1:1 копия классификатора id=4 (Ключевые слова) из isrib
        // (classificator_item), а не куррейтед-подмножество, как было раньше (19 строк). Решение
        // пользователя: перенести всё как есть в isrib, включая узкоспециализированные теги с
        // GUID-кодами — это осознанный выбор ради того, чтобы документы ВНД при переносе тянулись
        // с теми же ключевыми словами, что и в isrib. TitleEn не заполняется (в isrib нет перевода
        // на английский, придумывать не стали) — только TitleRu/TitleKg (name_rus/name_kyr) и
        // иерархия (ParentId вычислен из parent_code классификатора). Id — новые последовательные
        // (1..118), НЕ совпадают со старыми id куррейтед-версии — см. remap в
        // VndDocumentConfiguration.cs (demo vnd_keyword ссылался на старые id).
        // Примечание: некоторые названия в isrib повторяются под разными кодами — при живом поиске
        // по названию (KeywordRubricMapper) побеждает первое совпадение, это ожидаемо.
        builder.HasData(
            new { Id = 1, TitleRu = "Идентификация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Анализ", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "Продукт", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Возмещение расходов", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)12, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Сверка", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Ежемесячная сверка", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)5, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Платеж", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)5, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 8, TitleRu = "Ответственность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 9, TitleRu = "Планирование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)17, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 10, TitleRu = "Финансовое планирование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)17, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 11, TitleRu = "Бюджет", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 12, TitleRu = "Расход", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 13, TitleRu = "Безопасность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 14, TitleRu = "Матрица", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 15, TitleRu = "Непрерывная деятельность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)21, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 16, TitleRu = "Карточный продукт", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 17, TitleRu = "План", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 18, TitleRu = "Формирование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 19, TitleRu = "Резервный сервер", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 20, TitleRu = "Автоматизация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 21, TitleRu = "Непрерывность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 22, TitleRu = "Малоценные и быстроизнашивающиеся предметы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 23, TitleRu = "Корпоративный заемщик", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)73, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 24, TitleRu = "Корпоративный кредит", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 25, TitleRu = "Резерв", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 26, TitleRu = "Внутриобъектовый режим", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)50, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 27, TitleRu = "ГКО", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 28, TitleRu = "Ликвидность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 29, TitleRu = "Затраты", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 30, TitleRu = "Аккредитив", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 31, TitleRu = "Расследование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 32, TitleRu = "Тарифы на корпоративное кредитование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 33, TitleRu = "Рамочное соглашение", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 34, TitleRu = "Кредитная линия", TitleEn = (string?)null, TitleKg = "кредиттик линия", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 35, TitleRu = "Индивидуальный договор", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 36, TitleRu = "Управление корпоративного кредитования (УКК)", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 37, TitleRu = "Лизинг", TitleEn = (string?)null, TitleKg = "Лизинг", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 38, TitleRu = "Доступ", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 39, TitleRu = "Типовой договор", TitleEn = (string?)null, TitleKg = "Улгулуу Келишим", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 40, TitleRu = "Риск концентрации", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)66, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 41, TitleRu = "Корпоративное кредитование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 42, TitleRu = "Залог", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 43, TitleRu = "Платежные карты", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 44, TitleRu = "Кредитный лимит", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 45, TitleRu = "Тарифы", TitleEn = (string?)null, TitleKg = "Тарифтер", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 46, TitleRu = "Овердрафт", TitleEn = (string?)null, TitleKg = "Овердрафт", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 47, TitleRu = "Корреспондентский счет", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 48, TitleRu = "Ценообразование", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 49, TitleRu = "POS терминал", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 50, TitleRu = "Режим", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 51, TitleRu = "Расчеты", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 52, TitleRu = "Дисциплинарный проступок", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 53, TitleRu = "бизнес процесс", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 54, TitleRu = "Кассир", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 55, TitleRu = "Перевод", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 56, TitleRu = "Денежные переводы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 57, TitleRu = "Документ", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 58, TitleRu = "Инкассация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 59, TitleRu = "Операционный день", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 60, TitleRu = "Техподдержка", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 61, TitleRu = "Инсайдер", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 62, TitleRu = "Заявка", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 63, TitleRu = "Кредит", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 64, TitleRu = "Дебит", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 65, TitleRu = "Информационная безопасность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 66, TitleRu = "Риски", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 67, TitleRu = "Кассовые операции", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 68, TitleRu = "Аутсорсинг", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 69, TitleRu = "Залоговая стоимость", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)42, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 70, TitleRu = "Оформление", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 71, TitleRu = "Программное обеспечение", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 72, TitleRu = "Контрольный журнал", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 73, TitleRu = "Заемщик", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 74, TitleRu = "Смета", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 75, TitleRu = "Список инсайдеров", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)61, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 76, TitleRu = "Сберегательная касса", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 77, TitleRu = "Торгово-сервисное предприятие (ТСП)", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 78, TitleRu = "Банкомат", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 79, TitleRu = "ПФТ ОД", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 80, TitleRu = "ИТ-услуга", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 81, TitleRu = "Лимиты", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 82, TitleRu = "Иностранная валюта", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 83, TitleRu = "Хранение", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 84, TitleRu = "Компенсация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 85, TitleRu = "Банковская гарантия", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 86, TitleRu = "Подозрительная операция", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 87, TitleRu = "Оценка", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 88, TitleRu = "Антикризисное управление", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 89, TitleRu = "Платежный терминал", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 90, TitleRu = "Резервный центр", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 91, TitleRu = "Верификация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 92, TitleRu = "Конвертация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 93, TitleRu = "Имущество", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 94, TitleRu = "Устройство защитного обеспечения (УЗО)", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 95, TitleRu = "Авансовый отчет", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 96, TitleRu = "Дисциплинарное взыскание", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 97, TitleRu = "Пропускной режим", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)50, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 98, TitleRu = "Телефон", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 99, TitleRu = "Совокупная задолженность", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)73, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 100, TitleRu = "Фильтры", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 101, TitleRu = "Счета", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 102, TitleRu = "Норматив", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 103, TitleRu = "Залогодатель", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)42, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 104, TitleRu = "Инструкция", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 105, TitleRu = "Банковская информация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 106, TitleRu = "Начальник", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 107, TitleRu = "Приобретение", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 108, TitleRu = "Договор", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 109, TitleRu = "Операционный отдел", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 110, TitleRu = "Корреспондентские отношения", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 111, TitleRu = "ГКВ", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 112, TitleRu = "основные средства", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 113, TitleRu = "Платежное поручение", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 114, TitleRu = "Остатки", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 115, TitleRu = "Должностная инструкция", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 116, TitleRu = "Нематериальные активы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 117, TitleRu = "Каталог", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 118, TitleRu = "Информация", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}
