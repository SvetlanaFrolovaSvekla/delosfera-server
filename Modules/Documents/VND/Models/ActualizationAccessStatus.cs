namespace delosfera_server.Modules.Documents.VND.Models;

public enum ActualizationAccessStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Заявитель сам отозвал свою ещё не рассмотренную заявку (см.
    /// VndActualizationService.RevokeRequestAsync) - не путать с Rejected (решение
    /// главного редактора).</summary>
    Revoked = 3
}