using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Разово заводит глобальный шаблон маршрута служебной записки — визу руководителя
/// подразделения-инициатора (RoleRef=author-head).
///
/// Зачем: из коробки ни один вид записки не имел шаблона маршрута, и записка без
/// вручную названных согласующих не уходила на согласование вовсе — SubmitAsync
/// бросал «Не задан маршрут». Стандартный минимум — виза непосредственного
/// руководителя автора; администратор при необходимости переопределяет маршрут
/// (свой на подразделение или на вид записки) через конструктор согласующих.
///
/// Идемпотентно: если шаблон уровня типа для СЗ уже есть — не трогает.
/// </summary>
public static class SzRouteTemplateSeeder
{
    private const int StepTimeNormHours = 24;

    public static async Task SeedAsync(DelosferaDbContext db)
    {
        var exists = await db.RouteTemplates
            .AnyAsync(t => t.DocumentType == DocumentType.Sz && t.OrgUnitId == null);
        if (exists) return;

        db.RouteTemplates.Add(new RouteTemplate
        {
            DocumentType = DocumentType.Sz,
            Name = "Служебная записка — типовой маршрут",
            IsGlobalRule = true,
            OrgUnitId = null,
            Steps = new List<RouteTemplateStep>
            {
                new()
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Approval,
                    TimeNormHours = StepTimeNormHours,
                    Participants = new List<RouteTemplateParticipant>
                    {
                        new() { RoleRef = RouteRoles.AuthorHead, Required = true },
                    },
                },
            },
        });
        await db.SaveChangesAsync();
    }
}
