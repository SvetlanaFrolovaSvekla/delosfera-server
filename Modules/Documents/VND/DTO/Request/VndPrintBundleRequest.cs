namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Сборка одного .docx "для печати" из выбранных файлов редакции ВНД (кнопка в шапке
/// открытого ВНД рядом с глазиком и звёздочкой, см. VndPrintBundleModal на фронте).</summary>
public class VndPrintBundleRequest
{
    /// <summary>Файлы в том порядке, в котором они должны идти в итоговом документе: тексты
    /// редакции (RU/KG/EN), ТИД, листы согласования, матрица разногласий, вложения. Каждый файл
    /// обязан принадлежать одной из редакций этого ВНД.</summary>
    public List<int> FileIds { get; set; } = [];

    /// <summary>Редакция, для которой собирается документ, — только для имени итогового файла
    /// (необязательно).</summary>
    public int? RedactionId { get; set; }
}
