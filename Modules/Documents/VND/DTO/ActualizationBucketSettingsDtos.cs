namespace delosfera_server.Modules.Documents.VND.DTO;

/// <summary>Пороги индикации сроков актуализации ВНД (см. ActualizationBucket).
/// "Просрочено" сюда не входит — оно не настраивается.</summary>
public class ActualizationBucketSettingsResponse
{
    /// <summary>Ближе этого числа дней до срока — статус "Критично".</summary>
    public int CriticalDays { get; set; }

    /// <summary>До этого числа дней — статус "Приближается", дальше — "Норма".</summary>
    public int ApproachingDays { get; set; }
}

public class UpdateActualizationBucketSettingsRequest
{
    public int CriticalDays { get; set; }
    public int ApproachingDays { get; set; }
}
