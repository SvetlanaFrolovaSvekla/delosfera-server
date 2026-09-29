namespace delosfera_server.Modules.Signing.Services;

/// <summary>Настройки подписания резолюций ВНД ЭЦП (см. appsettings.json → EcpSigning).</summary>
public class EcpSigningOptions
{
    public const string Section = "EcpSigning";

    /// <summary>ТЕСТОВЫЙ режим: разрешить всем пользователям принимать решения по согласованию ВНД без
    /// ЭЦП (кнопки "... (без ЭЦП, тест)"). Только для тестовой среды — на бою должно быть false, тогда
    /// сервер отклонит решение без подписи (403), а клиент кнопки не покажет.</summary>
    public bool AllowWithoutEcp { get; set; }
}
