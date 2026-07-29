namespace delosfera_server.Modules.Dictionaries.DTO.Response;

/// <summary>
/// Краткая информация о пользователе — участнике группы
/// </summary>
public class UserGroupMemberResponse
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
}