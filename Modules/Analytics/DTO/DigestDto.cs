using delosfera_server.Modules.Workflow.DTO;

namespace delosfera_server.Modules.Analytics.DTO;

/// <summary>
/// Персональный дайджест (УВ-14): одна сводка «что требует моего внимания» по всем
/// контурам. Собирается из единого реестра задач — то же, что человек видит в «Мои
/// задачи», но свёрнуто в короткую выжимку: сколько всего, что просрочено, что горит
/// в ближайшие двое суток и на чём сосредоточиться в первую очередь.
/// </summary>
public class DigestDto
{
    public int Total { get; set; }
    public int Overdue { get; set; }

    /// <summary>Срок наступает в ближайшие 48 часов (ещё не просрочено).</summary>
    public int DueSoon { get; set; }

    /// <summary>Сколько задач получено по замещению.</summary>
    public int Delegated { get; set; }

    public List<DigestContourDto> Contours { get; set; } = [];

    /// <summary>Ближайшие по сроку задачи — на чём сосредоточиться.</summary>
    public List<InboxTaskDto> Upcoming { get; set; } = [];

    /// <summary>Просроченные задачи — что горит.</summary>
    public List<InboxTaskDto> OverdueItems { get; set; } = [];
}

/// <summary>Сводка по одному контуру в дайджесте.</summary>
public class DigestContourDto
{
    public required string Contour { get; set; }
    public required string Title { get; set; }
    public int Count { get; set; }
    public int Overdue { get; set; }
}
