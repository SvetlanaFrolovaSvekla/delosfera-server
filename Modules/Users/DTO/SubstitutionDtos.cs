namespace delosfera_server.Modules.Users.DTO;

/// <summary>Замещение сотрудника (GEN-14).</summary>
public class SubstitutionDto
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public required string UserName { get; set; }

    public int SubstituteUserId { get; set; }
    public required string SubstituteUserName { get; set; }

    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string? Reason { get; set; }

    public bool IsCancelled { get; set; }

    /// <summary>Период идёт прямо сейчас — задачи перенаправляются.</summary>
    public bool IsActive { get; set; }
}

/// <summary>Оформление замещения на период отсутствия.</summary>
public class SubstitutionCreateRequest
{
    public int UserId { get; set; }
    public int SubstituteUserId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string? Reason { get; set; }
}
