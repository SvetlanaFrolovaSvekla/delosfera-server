namespace delosfera_server.Modules.Sz.Services;

/// <summary>Как поле выглядит и что в него можно ввести.</summary>
public static class HrFieldType
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Money = "money";
    public const string Date = "date";
    public const string Checkbox = "checkbox";
    public const string Select = "select";

    /// <summary>Подразделение из справочника.</summary>
    public const string OrgUnit = "orgUnit";
}

/// <summary>Описание одного поля формы.</summary>
public class HrField
{
    public required string Code { get; set; }
    public required string Label { get; set; }
    public required string Type { get; set; }

    /// <summary>Без него записку нельзя отправить на согласование.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Поле заполняется по каждому сотруднику отдельно, а не одно на записку.
    /// Оклад у каждого свой, а город командировки — общий.
    /// </summary>
    public bool PerEmployee { get; set; }

    /// <summary>Варианты для выбора.</summary>
    public List<string>? Options { get; set; }

    /// <summary>Пояснение под полем — когда из названия непонятно, что вписывать.</summary>
    public string? Hint { get; set; }
}

/// <summary>Форма для одного вида кадровой записки.</summary>
public class HrForm
{
    public required string HrKindTitle { get; set; }

    /// <summary>Можно ли указать нескольких сотрудников.</summary>
    public bool AllowMultipleEmployees { get; set; }

    /// <summary>
    /// Сотрудника нет в системе — записка о приёме. Тогда ФИО вводится строкой,
    /// а не выбирается из справочника.
    /// </summary>
    public bool EmployeeMayBeExternal { get; set; }

    public List<HrField> Fields { get; set; } = [];
}

/// <summary>
/// Какие поля показывать для каждого вида кадровой записки.
///
/// До этого форма была одна на все виды: вид записки, ФИО, подразделение сотрудника
/// и подразделение перевода. Для командировки в ней не было ни срока, ни города;
/// для изменения оклада — ни старой суммы, ни новой; для приёма на работу — ни
/// должности, ни даты выхода. Всё это писали в текст записки, откуда ни отобрать,
/// ни посчитать.
///
/// Схема лежит в коде, а не в справочнике: состав полей задан кадровой политикой,
/// и менять его должен тот, кто отвечает за политику, а не тот, у кого есть доступ
/// к справочникам. Когда набор устоится, его можно вынести в настройки.
///
/// Ключ — название вида из справочника sz_hr_kind. Названия сверяются по-русски и
/// без учёта регистра: справочник ведёт делопроизводство, и переименование вида не
/// должно ронять форму.
/// </summary>
public static class HrFormSchema
{
    private static readonly HrField Salary = new()
    {
        Code = "salaryNew", Label = "Новый оклад", Type = HrFieldType.Money,
        Required = true, PerEmployee = true,
    };

    private static readonly HrField SalaryOld = new()
    {
        Code = "salaryOld", Label = "Текущий оклад", Type = HrFieldType.Money,
        PerEmployee = true, Hint = "Заполняется, если известен — для сравнения",
    };

    private static readonly HrField EffectiveFrom = new()
    {
        Code = "effectiveFrom", Label = "С какой даты", Type = HrFieldType.Date, Required = true,
    };

    private static readonly HrField Reason = new()
    {
        Code = "reason", Label = "Основание", Type = HrFieldType.Text,
        Hint = "Решение, служебная записка, итоги оценки",
    };

    public static IReadOnlyDictionary<string, HrForm> Forms { get; } =
        new Dictionary<string, HrForm>(StringComparer.OrdinalIgnoreCase)
        {
            ["Изменение оклада"] = new()
            {
                HrKindTitle = "Изменение оклада",
                AllowMultipleEmployees = true,
                Fields = [SalaryOld, Salary, EffectiveFrom, Reason],
            },

            ["Командировка"] = new()
            {
                HrKindTitle = "Командировка",
                AllowMultipleEmployees = true,
                Fields =
                [
                    new() {Code = "destination", Label = "Куда", Type = HrFieldType.Text, Required = true,
                           Hint = "Город, страна, принимающая организация"},
                    new() {Code = "purpose", Label = "Цель поездки", Type = HrFieldType.Text, Required = true},
                    new() {Code = "dateFrom", Label = "С", Type = HrFieldType.Date, Required = true},
                    new() {Code = "dateTo", Label = "По", Type = HrFieldType.Date, Required = true},
                    new() {Code = "expenses", Label = "Командировочные расходы", Type = HrFieldType.Checkbox,
                           Hint = "Проезд, проживание, суточные за счёт банка"},
                    new() {Code = "expensesAmount", Label = "Сумма расходов", Type = HrFieldType.Money,
                           Hint = "Ориентировочно — по ней идёт бюджетный контроль"},
                    new() {Code = "fundingSource", Label = "Источник финансирования", Type = HrFieldType.Text},
                ],
            },

            ["Перемещение с изменением оклада"] = new()
            {
                HrKindTitle = "Перемещение с изменением оклада",
                Fields =
                [
                    new() {Code = "positionNew", Label = "Новая должность", Type = HrFieldType.Text, Required = true},
                    new() {Code = "unitTo", Label = "В подразделение", Type = HrFieldType.OrgUnit, Required = true},
                    SalaryOld, Salary, EffectiveFrom, Reason,
                ],
            },

            ["Перемещение без изменения оклада"] = new()
            {
                HrKindTitle = "Перемещение без изменения оклада",
                Fields =
                [
                    new() {Code = "positionNew", Label = "Новая должность", Type = HrFieldType.Text, Required = true},
                    new() {Code = "unitTo", Label = "В подразделение", Type = HrFieldType.OrgUnit, Required = true},
                    EffectiveFrom, Reason,
                ],
            },

            ["Приём на работу"] = new()
            {
                HrKindTitle = "Приём на работу",
                AllowMultipleEmployees = true,
                EmployeeMayBeExternal = true,
                Fields =
                [
                    new() {Code = "positionNew", Label = "Должность", Type = HrFieldType.Text,
                           Required = true, PerEmployee = true},
                    new() {Code = "unitTo", Label = "Подразделение", Type = HrFieldType.OrgUnit, Required = true},
                    Salary,
                    new() {Code = "startDate", Label = "Дата выхода", Type = HrFieldType.Date, Required = true},
                    new() {Code = "probation", Label = "Испытательный срок", Type = HrFieldType.Select,
                           Options = ["без испытательного срока", "1 месяц", "2 месяца", "3 месяца"]},
                    new() {Code = "employmentType", Label = "Вид занятости", Type = HrFieldType.Select,
                           Options = ["основное место", "совместительство", "срочный договор"]},
                ],
            },

            ["Приём стажёров с оплатой"] = new()
            {
                HrKindTitle = "Приём стажёров с оплатой",
                AllowMultipleEmployees = true,
                EmployeeMayBeExternal = true,
                Fields =
                [
                    new() {Code = "unitTo", Label = "Подразделение", Type = HrFieldType.OrgUnit, Required = true},
                    new() {Code = "mentor", Label = "Наставник", Type = HrFieldType.Text, Required = true},
                    new() {Code = "dateFrom", Label = "С", Type = HrFieldType.Date, Required = true},
                    new() {Code = "dateTo", Label = "По", Type = HrFieldType.Date, Required = true},
                    new() {Code = "stipend", Label = "Размер оплаты", Type = HrFieldType.Money,
                           Required = true, PerEmployee = true},
                ],
            },

            ["Приём стажёров без оплаты"] = new()
            {
                HrKindTitle = "Приём стажёров без оплаты",
                AllowMultipleEmployees = true,
                EmployeeMayBeExternal = true,
                Fields =
                [
                    new() {Code = "unitTo", Label = "Подразделение", Type = HrFieldType.OrgUnit, Required = true},
                    new() {Code = "mentor", Label = "Наставник", Type = HrFieldType.Text, Required = true},
                    new() {Code = "dateFrom", Label = "С", Type = HrFieldType.Date, Required = true},
                    new() {Code = "dateTo", Label = "По", Type = HrFieldType.Date, Required = true},
                ],
            },

            ["Работа в выходной день"] = new()
            {
                HrKindTitle = "Работа в выходной день",
                AllowMultipleEmployees = true,
                Fields =
                [
                    new() {Code = "workDate", Label = "Дата выхода", Type = HrFieldType.Date, Required = true},
                    new() {Code = "hours", Label = "Часов работы", Type = HrFieldType.Number},
                    new() {Code = "workReason", Label = "Причина выхода", Type = HrFieldType.Text, Required = true},
                    new() {Code = "compensation", Label = "Компенсация", Type = HrFieldType.Select,
                           Required = true, Options = ["отгул", "оплата в двойном размере"]},
                ],
            },

            ["Другое"] = new()
            {
                HrKindTitle = "Другое",
                AllowMultipleEmployees = true,
                Fields = [EffectiveFrom, Reason],
            },
        };

    /// <summary>
    /// Форма по названию вида. Неизвестный вид — не ошибка: делопроизводство может
    /// завести новый, и записка должна открыться, пусть и без особых полей.
    /// </summary>
    public static HrForm? For(string? hrKindTitle) =>
        string.IsNullOrWhiteSpace(hrKindTitle) ? null : Forms.GetValueOrDefault(hrKindTitle.Trim());
}
