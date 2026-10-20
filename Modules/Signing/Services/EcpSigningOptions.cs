namespace delosfera_server.Modules.Signing.Services;

/// <summary>Настройки подписания резолюций ВНД ЭЦП (см. appsettings.json → EcpSigning).</summary>
public class EcpSigningOptions
{
    public const string Section = "EcpSigning";

    /// <summary>ТЕСТОВЫЙ режим: разрешить всем пользователям принимать решения по согласованию ВНД без
    /// ЭЦП (кнопки "... (без ЭЦП, тест)"). Только для тестовой среды — на бою должно быть false, тогда
    /// сервер отклонит решение без подписи (403), а клиент кнопки не покажет.</summary>
    public bool AllowWithoutEcp { get; set; }

    /// <summary>ТЕСТОВЫЙ режим только для служебных записок: кнопки «… (тест ЭЦП)» имитируют весь путь
    /// подписания (ПИН, код, токен, подпись) без обращения к ГП «Инфоком». Решение сохраняется с
    /// «подписью»-имитацией: в журнале СЗ она помечена Verified=false и «ТЕСТ», в листе согласования —
    /// штампом «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП». Включается только в appsettings.Development.json или переменной
    /// окружения EcpSigning__AllowSzTestEcp=true; на бою должно быть false.</summary>
    public bool AllowSzTestEcp { get; set; }

    /// <summary>ТЕСТОВЫЙ режим для согласования ВНД — то же, что <see cref="AllowSzTestEcp"/> для СЗ:
    /// кнопки «… (тест ЭЦП)» имитируют подписание без обращения к ГП «Инфоком». Решение сохраняется с
    /// «подписью»-имитацией в журнале VndResolutionSignature (Verified=false, «ТЕСТ»), в листе
    /// согласования с ЭЦП — штамп «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП». Включается только в
    /// appsettings.Development.json или переменной окружения EcpSigning__AllowVndTestEcp=true; на бою
    /// должно быть false.</summary>
    public bool AllowVndTestEcp { get; set; }

    /// <summary>ТЕСТОВЫЙ режим для подписания приказа и договора МО заявок на замещение — отдельно от
    /// СЗ и ВНД: кнопки «Тест» ставят «подпись»-имитацию без обращения к ГП «Инфоком» (в журнале
    /// SubstitutionSignature — Verified=false, «ТЕСТ»; в документе — штамп «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП»).
    /// Включается только на тестовом стенде — переменной окружения
    /// EcpSigning__AllowSubstitutionTestEcp=true (или в appsettings.Development.json); на бою false.</summary>
    public bool AllowSubstitutionTestEcp { get; set; }
}
