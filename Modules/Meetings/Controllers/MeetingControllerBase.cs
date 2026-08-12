using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Meetings.Controllers;

/// <summary>
/// Разбор доменных исключений в коды ответа — общий для контроллеров раздела.
///
/// Отдельный базовый класс, а не копия метода в каждом контроллере: в заседаниях
/// половина операций упирается в права (кто секретарь, кто исполнитель), и отказ
/// должен возвращаться как 403 с внятным текстом, а не как 500.
/// </summary>
public abstract class MeetingControllerBase : ControllerBase
{
    protected async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
