namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Оценка работы поставщика по итогам закупки/договора (ЗК-9).
///
/// Благонадёжность и чёрный список отвечают на вопрос «можно ли вообще с ним
/// работать»; рейтинг — на «как он работал на деле»: сроки, качество, дисциплина
/// поставки. Копится по договорам, чтобы при следующем отборе видеть не только
/// цену, но и историю исполнения, а не полагаться на память инициатора.
/// </summary>
public class SupplierRating
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Кто поставил оценку.</summary>
    public int AuthorUserId { get; set; }

    /// <summary>
    /// Договор, по которому выставлена оценка. Необязателен: оценку можно поставить и
    /// по разовой закупке без договора, но привязка к договору делает её проверяемой.
    /// </summary>
    public int? ContractId { get; set; }

    /// <summary>Оценка 1..5.</summary>
    public int Score { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}
