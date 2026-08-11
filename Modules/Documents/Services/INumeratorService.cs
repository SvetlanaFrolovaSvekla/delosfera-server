using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Настраиваемые нумераторы регистрации (GEN-09).
/// </summary>
public interface INumeratorService
{
    /// <summary>
    /// Выдать следующий регистрационный номер для (тип, область, ключ области),
    /// создавая нумератор при первом обращении. Плейсхолдеры шаблона: {seq[:формат]},
    /// {year}, плюс произвольные из <paramref name="tokens"/> (например {unit}).
    /// </summary>
    Task<string> NextAsync(
        DocumentType type, string scope, string scopeKey, string pattern,
        IReadOnlyDictionary<string, string>? tokens = null);
}
