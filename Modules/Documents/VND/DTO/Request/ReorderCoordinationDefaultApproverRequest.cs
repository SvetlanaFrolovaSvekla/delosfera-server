namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Новый порядок этапов справочника - полный список id всех существующих записей
/// в желаемом порядке (drag-and-drop на фронте). Должен содержать ровно те же id, что сейчас
/// есть в справочнике, каждый ровно один раз.</summary>
public class ReorderCoordinationDefaultApproverRequest
{
    public required List<int> OrderedIds { get; set; }
}
