namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndTaskCountsResponse
{
    /// <summary>"Ждущие моего согласования" — первичное + повторное согласование + финальная выдержка.</summary>
    public int Coordination { get; set; }
    public int Actualization { get; set; }
    public int Consolidation { get; set; }
    public int MyVndApproval { get; set; }

    /// <summary>Редакции, отклонённые при согласовании и ожидающие правок инициатора.</summary>
    public int Rejected { get; set; }
}
