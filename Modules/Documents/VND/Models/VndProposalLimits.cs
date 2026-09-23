namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Ограничения на предложение по ВНД — общие для конфигурации БД и проверок в сервисе.</summary>
public static class VndProposalLimits
{
    public const int MaxTextLength = 5000;
    public const int MaxQuotes = 30;
    public const int MaxQuoteTextLength = 1000;
    public const int MaxQuoteNoteLength = 2000;
    public const int MaxAttachments = 10;
    public const long MaxAttachmentSizeBytes = 50L * 1024 * 1024;
    /// <summary>Не больше стольких предложений от одного человека за час — защита от залипшей
    /// кнопки/повторной отправки в цикле, не от недоверия.</summary>
    public const int MaxPerHour = 20;
}
