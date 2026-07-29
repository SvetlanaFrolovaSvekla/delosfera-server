namespace delosfera_server.Modules.Vnd.Models;

public enum VndStatus
{
    Active = 0,  // действующий
    OnActualization = 1, // на актуализации
    Review = 2,  // на согласовании
    Consolidation = 3, // на консолидации
    Archived = 4,  // архивирован
    Draft = 5,           // черновик — только что создан, редакция ещё не загружена
}