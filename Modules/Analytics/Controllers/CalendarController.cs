using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Analytics.Services;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>Календарь сроков по всем контурам (ЗС-13).</summary>
[ApiController]
[Route("api/calendar")]
[Tags("Календарь сроков")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendar;
    private readonly ICurrentUserService _currentUser;

    public CalendarController(ICalendarService calendar, ICurrentUserService currentUser)
    {
        _calendar = calendar;
        _currentUser = currentUser;
    }

    /// <summary>Мои датированные задачи для сетки месяца.</summary>
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _calendar.GetAsync(_currentUser.UserId));
}
