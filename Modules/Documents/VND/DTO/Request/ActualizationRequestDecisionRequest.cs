namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ActualizationRequestDecisionRequest
{
    public required bool Approve { get; set; }

    /// <summary>Финальное значение сдвига срока — обязательно, только когда Approve == true.
    /// По умолчанию на фронте предзаполняется тем, что выбрал сам заявитель (см.
    /// VndActualizationRequest.ShiftNextPeriod), но главный редактор может его изменить перед
    /// одобрением. Игнорируется при отклонении заявки.</summary>
    public bool? ShiftNextPeriod { get; set; }
}
