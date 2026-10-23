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

    /// <summary>Та же сводка оформленной книгой Excel: сводка, три разреза и перечень записок.</summary>
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

        // Имя кириллицей: ASP.NET кладёт его в filename*=UTF-8'' (RFC 5987), а в обычный
        // filename= - ASCII-заглушку из подчёркиваний; клиент берёт именно filename*.
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SzStatisticsService.ExportFileName(from, to));
    }
}
