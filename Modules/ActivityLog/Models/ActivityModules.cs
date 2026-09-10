namespace delosfera_server.Modules.ActivityLog.Models;

// Типы действий (связанные с типом документов, деятельности)
public static class ActivityModules
{
    public const string Vnd = "vnd";

    /// <summary>Служебные записки. Собственного потока журнала не ведут — события
    /// берутся из технического аудита (тип "Sz").</summary>
    public const string Sz = "sz";

    /// <summary>Закупки. Собственного потока журнала не ведут — события берутся из
    /// технического аудита (тип "ProcurementRequest").</summary>
    public const string Procurement = "prc";
}