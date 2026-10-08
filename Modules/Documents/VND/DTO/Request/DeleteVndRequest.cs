namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Тело DELETE /api/vnd/{id}: пароль текущего пользователя — повторное подтверждение удаления.</summary>
public class DeleteVndRequest
{
    public string? Password { get; set; }
}
