namespace delosfera_server.Common.Services;

/// <summary>
/// Календарные даты в часовом поясе банка.
///
/// Сроки исполнения, периоды замещения и даты документов — календарные: сотрудник
/// оформляет отпуск «с 12 августа», имея в виду местную дату. Сравнение с UTC даёт
/// расхождение до шести часов (Бишкек — UTC+6): замещение, начинающееся сегодня,
/// включилось бы только к обеду, а документ, созданный вечером, получил бы вчерашнюю дату.
/// </summary>
public interface IBankClock
{
    /// <summary>Сегодняшняя дата по времени банка.</summary>
    DateOnly Today { get; }

    /// <summary>Текущий момент по времени банка.</summary>
    DateTime Now { get; }
}

public class BankClock : IBankClock
{
    /// <summary>
    /// Часовой пояс банка. Вынесен константой: при появлении филиалов в другом поясе
    /// значение переедет в конфигурацию, а формула сравнения останется прежней.
    /// </summary>
    private const string TimeZoneId = "Asia/Bishkek";

    private static readonly TimeZoneInfo Zone = Resolve();

    public DateOnly Today => DateOnly.FromDateTime(Now);

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // На системах без базы IANA (например, Windows без ICU) остаётся смещение:
            // Кыргызстан живёт на UTC+6 круглый год, перевода часов нет.
            return TimeZoneInfo.CreateCustomTimeZone("KG", TimeSpan.FromHours(6), "Kyrgyzstan", "Kyrgyzstan");
        }
    }
}
