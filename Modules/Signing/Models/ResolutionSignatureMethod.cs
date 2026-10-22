namespace delosfera_server.Modules.Signing.Models;

/// <summary>Чем поставлена подпись под резолюцией (журналы подписей ВНД, а в дальнейшем СЗ и
/// замещений). Хранится числом — значения не переиспользовать.</summary>
public enum ResolutionSignatureMethod
{
    /// <summary>Облачная ЭЦП ГП «Инфоком» (Түндүк): подпись поставлена и проверена у провайдера.
    /// Тестовая имитация ЭЦП (EcpSigning:AllowVndTestEcp) тоже хранится с этим значением и отличается
    /// по Verified=false + VerificationError (см. ResolutionSignatureService.IsTestSignature).</summary>
    InfocomEcp = 0,

    /// <summary>Локальная подпись (печать) системы «Делосфера» — для сотрудников без ЭЦП КР
    /// (право SignWithoutNationalEcp). Подпись — HMAC-SHA256 ключом сервера (SystemSeal:HmacKey)
    /// поверх хэша резолюции, к ГП «Инфоком» не обращаемся. Юридически это не ЭЦП КР: штамп и
    /// журналы прямо пишут, что подпись поставлена системой.</summary>
    DelosferaSystem = 1,
}
