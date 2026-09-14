namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Сохранённый фильтр реестра (БП-16): именованный набор условий поиска, который
/// пользователь применяет одним кликом вместо того, чтобы каждый раз проставлять их
/// заново.
///
/// Личный — принадлежит пользователю: у каждого свой набор регулярных выборок («мои
/// на согласовании», «просроченные по моему отделу»). Привязан к области (scope):
/// фильтр реестра записок бессмысленно применять к реестру закупок.
///
/// Условия лежат строкой JSON, а не колонками: набор полей у каждого реестра свой и
/// меняется, а сохранённый фильтр лишь возвращает значения в форму поиска.
/// </summary>
public class SavedFilter
{
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    /// <summary>Область применения: sz | procurement (реестр, к которому фильтр относится).</summary>
    public required string Scope { get; set; }

    public required string Name { get; set; }

    /// <summary>Условия фильтра, сериализованные JSON.</summary>
    public required string Payload { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
