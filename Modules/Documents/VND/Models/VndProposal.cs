using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Предложение по ВНД — сообщение любого сотрудника главному редактору ВНД о том, что в
/// документе стоит изменить/дополнить (кнопка "+ Предложения по ВНД" на странице открытого ВНД).
///
/// К тексту можно приложить файлы (<see cref="Attachments"/>) и цитаты из текста редакции
/// (<see cref="Quotes"/>, "+ Сослаться на текст редакции") — без привязки к месту в тексте
/// ("Показать в тексте" для предложений нет, только сама цитата).
///
/// Получатели — пользователи с правом <see cref="PermissionCode.ManageVndProposals"/> (главный
/// редактор ВНД): им приходит системное уведомление и письмо, а сами предложения собраны на
/// странице "Нормотворчество (ВНД)" → "Предложения по ВНД". Статус "прочитано" — общий для всех
/// получателей: достаточно, чтобы предложение разобрал кто-то один.</summary>
public class VndProposal
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    /// <summary>Редакция, которую смотрел автор, когда писал предложение (и из которой брал
    /// цитаты). null — редакции нет/удалена.</summary>
    public int? RedactionId { get; set; }
    public VndRedaction? Redaction { get; set; }

    public int AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Когда предложение впервые прочитал получатель (главный редактор). null — не прочитано.</summary>
    public DateTime? ReadAt { get; set; }
    public int? ReadByUserId { get; set; }
    public User? ReadByUser { get; set; }

    public List<VndProposalQuote> Quotes { get; set; } = [];
    public List<VndProposalAttachment> Attachments { get; set; } = [];
}
