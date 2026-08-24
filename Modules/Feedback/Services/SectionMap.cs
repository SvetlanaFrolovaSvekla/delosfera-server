namespace delosfera_server.Modules.Feedback.Services;

/// <summary>
/// Человеческое название раздела по маршруту экрана.
///
/// В отчёте о посещаемости показывают разделы, а не адреса: «Нормативные
/// документы» вместо «/base-vnd/:id». Один раздел собирает несколько маршрутов —
/// список ВНД, карточка и создание это одно место в глазах сотрудника, и считать
/// их порознь значит дробить цифру, которую собирались смотреть.
///
/// Порядок важен: ищется первое совпадение, поэтому частные маршруты стоят
/// раньше общих. «/hr/orders» должен найтись до «/hr».
/// </summary>
public static class SectionMap
{
    private static readonly (string Prefix, string Title)[] Sections =
    [
        ("/base-vnd", "Нормативные документы"),
        ("/actualization", "План актуализации"),
        ("/reportvnd", "Отчётность по ВНД"),

        ("/sz-analytics", "Аналитика записок"),
        ("/sz", "Служебные записки"),

        ("/prc", "Закупки"),

        ("/meetings/candidates", "Отбор вопросов"),
        ("/meetings", "Заседания"),

        ("/correspondence", "Корреспонденция"),
        ("/poa", "Доверенности"),
        ("/obligations", "Регулярные обязательства"),

        ("/hr/orders", "Кадровые приказы"),
        ("/hr-ack", "Ознакомление"),

        ("/inbox", "Мои задачи"),
        ("/tasks", "Задачи по ВНД"),

        // Настройки переехали под «Управление». Частные адреса стоят раньше
        // общего «/management», иначе всё слилось бы в один раздел.
        ("/management/users", "Сотрудники"),
        ("/management/roles", "Доступы и роли"),
        ("/management/substitutions", "Замещения"),
        ("/management/refs", "Справочники"),
        ("/management/audit", "Журнал действий"),
        ("/management/changes", "Журнал изменений"),
        ("/management/usage", "Посещения портала"),
        ("/management/feedback", "Пожелания и замечания"),
        ("/management/integrations", "Интеграции"),
        ("/management/signing", "Настройки подписания"),
        ("/management/help", "Инструкции"),
        ("/management/obligations", "Регулярные обязательства"),
        ("/management", "Управление"),

        ("/signing-workplace", "Рабочее место подписи"),
        ("/help", "Инструкции"),
        ("/search", "Поиск"),
        ("/notifications", "Уведомления"),
        ("/profile", "Профиль"),
    ];

    /// <summary>
    /// Раздел по маршруту. Корень — «Главная»; неизвестный маршрут возвращается
    /// как есть, чтобы новый экран не потерялся в куче «прочего», а был виден
    /// и попал в этот список при следующей правке.
    /// </summary>
    public static string Title(string routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath) || routePath == "/")
            return "Главная";

        foreach (var (prefix, title) in Sections)
        {
            if (routePath == prefix || routePath.StartsWith(prefix + "/", StringComparison.Ordinal))
                return title;
        }

        return routePath;
    }
}
