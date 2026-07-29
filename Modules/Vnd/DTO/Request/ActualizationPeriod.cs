namespace delosfera_server.Modules.Vnd.DTO.Request;

public enum ActualizationPeriod
{
    Quarterly,   // 3 месяца
    HalfYear,    // 6 месяцев
    Annual,      // 12 месяцев
    Biennial,    // 24 месяца
    Triennial,   // 36 месяцев
    Custom       // явная дата DueActualizationDate
}

/*public enum ActualizationPeriod {
    Quarterly = 0, // 3 месяца
    HalfYear = 1, // 6 месяцев
    Annual = 2, // 12 месяцев
    Biennial = 3, // 24 месяца
    Triennial = 4, // 36 месяцев
    Custom = 5, // явная дата DueActualizationDate
}*/