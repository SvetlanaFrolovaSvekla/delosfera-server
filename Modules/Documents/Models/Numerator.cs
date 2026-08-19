namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Настраиваемый нумератор регистрации документов (GEN-09). Администрируется
/// без кода. Формат — шаблон с плейсхолдерами: {type},{unit},{year},{seq}.
/// </summary>
public class Numerator
{
    public int Id { get; set; }

    public DocumentType DocumentType { get; set; }

    /// <summary>Область действия счётчика (например "Global", "PerYear", "PerUnitYear").</summary>
    public required string Scope { get; set; }

    /// <summary>Ключ области (напр. "2026" или "2026:UnitId=5"), задаёт отдельную последовательность.</summary>
    public required string ScopeKey { get; set; }

    /// <summary>Шаблон номера, напр. "СЗ-{unit}-{year}-{seq:0000}".</summary>
    public required string Pattern { get; set; }

    /// <summary>Следующее значение счётчика.</summary>
    public int NextSeq { get; set; } = 1;
}
