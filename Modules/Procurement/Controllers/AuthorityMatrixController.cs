using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Матрица полномочий по закупкам (PRC-02/04/05): подбор способа закупки по сумме
/// и приложение №1 Положения целиком.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Матрица полномочий")]
[Authorize]
public class AuthorityMatrixController : ControllerBase
{
    private readonly IAuthorityMatrixService _matrix;

    public AuthorityMatrixController(IAuthorityMatrixService matrix) => _matrix = matrix;

    /// <summary>Матрица целиком с пересчётом процентных порогов в сомы.</summary>
    [HttpGet("matrix")]
    public async Task<IActionResult> Table() => Ok(await _matrix.GetTableAsync());

    /// <summary>Подобрать способ закупки, состав согласования и орган утверждения по сумме.</summary>
    [HttpPost("matrix/resolve")]
    public async Task<IActionResult> Resolve([FromBody] MatrixResolveRequest request)
    {
        try
        {
            return Ok(await _matrix.ResolveAsync(request));
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
