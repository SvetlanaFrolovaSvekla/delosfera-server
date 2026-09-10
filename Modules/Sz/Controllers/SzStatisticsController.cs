using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>Статистика по служебным запискам и её выгрузка в Excel (SZ-06).</summary>
/// <remarks>Сводка охватывает записки всех подразделений, поэтому доступна только
/// обладателям права «видеть все записки» (делопроизводство, руководство). Прежде
/// эндпоинты были открыты любому аутентифицированному пользователю.</remarks>
[ApiController]
[Route("api/sz/statistics")]
[Tags("Служебные записки — статистика")]
[Authorize]
[RequirePermission(PermissionCode.ViewAllSz)]
public class SzStatisticsController : ControllerBase
{
    private readonly ISzStatisticsService _statistics;

    public SzStatisticsController(ISzStatisticsService statistics) => _statistics = statistics;

    /// <summary>Сводка: в работе, просрочено, исполнено — по подразделениям, видам и месяцам.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? orgUnitId,
        [FromQuery] int? kindId) =>
        Ok(await _statistics.GetAsync(new SzStatisticsFilter
        {
            From = from, To = to, OrgUnitId = orgUnitId, KindId = kindId,
        }));

    /// <summary>Та же сводка книгой Excel: четыре листа, включая перечень записок.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? orgUnitId,
        [FromQuery] int? kindId)
    {
        var bytes = await _statistics.ExportAsync(new SzStatisticsFilter
        {
            From = from, To = to, OrgUnitId = orgUnitId, KindId = kindId,
        });

        var period = from is null && to is null
            ? "за всё время"
            : $"{from?.ToString("dd.MM.yyyy") ?? "начало"}-{to?.ToString("dd.MM.yyyy") ?? "сегодня"}";

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Статистика СЗ {period}.xlsx");
    }
}
