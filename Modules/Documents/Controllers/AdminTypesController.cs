using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Dictionaries.DTO;
using delosfera_server.Modules.Dictionaries.Services;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>
/// Настройка системы силами администратора заказчика: типы документов, справочники
/// и представления журналов (GEN-06, GEN-07, GEN-10).
/// </summary>
[ApiController]
[Route("api/admin")]
[Tags("Администрирование — типы, справочники, представления")]
[Authorize]
public class AdminTypesController : ControllerBase
{
    private readonly IDocumentTypeDefinitionService _types;
    private readonly ICustomDictionaryService _dictionaries;
    private readonly IListViewService _views;
    private readonly ICurrentUserService _currentUser;

    public AdminTypesController(
        IDocumentTypeDefinitionService types,
        ICustomDictionaryService dictionaries,
        IListViewService views,
        ICurrentUserService currentUser)
    {
        _types = types;
        _dictionaries = dictionaries;
        _views = views;
        _currentUser = currentUser;
    }

    // ── Типы документов (GEN-06) ─────────────────────────────────────────────

    /// <summary>Список настраиваемых типов документов.</summary>
    [HttpGet("document-types")]
    public async Task<IActionResult> Types([FromQuery] bool includeInactive = false) =>
        await Run(() => _types.ListAsync(includeInactive));

    [HttpGet("document-types/{id:int}")]
    public async Task<IActionResult> Type(int id) => await Run(() => _types.GetAsync(id));

    [HttpPost("document-types")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> CreateType([FromBody] DocumentTypeSaveRequest request) =>
        await Run(() => _types.CreateAsync(request));

    [HttpPut("document-types/{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> UpdateType(int id, [FromBody] DocumentTypeSaveRequest request) =>
        await Run(() => _types.UpdateAsync(id, request));

    [HttpDelete("document-types/{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> DeleteType(int id) =>
        await Run(async () => { await _types.DeleteAsync(id); return true; });

    [HttpPost("document-types/{id:int}/fields")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> AddField(int id, [FromBody] DocumentTypeFieldRequest request) =>
        await Run(() => _types.AddFieldAsync(id, request));

    [HttpPut("document-types/fields/{fieldId:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> UpdateField(int fieldId, [FromBody] DocumentTypeFieldRequest request) =>
        await Run(() => _types.UpdateFieldAsync(fieldId, request));

    [HttpDelete("document-types/fields/{fieldId:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> DeleteField(int fieldId) =>
        await Run(() => _types.DeleteFieldAsync(fieldId));

    // ── Справочники администратора (GEN-07) ──────────────────────────────────

    [HttpGet("dictionaries")]
    public async Task<IActionResult> Dictionaries([FromQuery] bool includeInactive = false) =>
        await Run(() => _dictionaries.ListAsync(includeInactive));

    [HttpGet("dictionaries/{id:int}")]
    public async Task<IActionResult> Dictionary(int id) => await Run(() => _dictionaries.GetAsync(id));

    /// <summary>Справочник по системному имени — по нему на него ссылаются поля типов.</summary>
    [HttpGet("dictionaries/by-code/{code}")]
    public async Task<IActionResult> DictionaryByCode(string code) =>
        await Run(() => _dictionaries.GetByCodeAsync(code));

    [HttpPost("dictionaries")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> CreateDictionary([FromBody] CustomDictionarySaveRequest request) =>
        await Run(() => _dictionaries.CreateAsync(request));

    [HttpPut("dictionaries/{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> UpdateDictionary(int id, [FromBody] CustomDictionarySaveRequest request) =>
        await Run(() => _dictionaries.UpdateAsync(id, request));

    [HttpDelete("dictionaries/{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> DeleteDictionary(int id) =>
        await Run(async () => { await _dictionaries.DeleteAsync(id); return true; });

    [HttpPost("dictionaries/{id:int}/items")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> AddItem(int id, [FromBody] CustomDictionaryItemRequest request) =>
        await Run(() => _dictionaries.AddItemAsync(id, request));

    [HttpPut("dictionaries/items/{itemId:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> UpdateItem(int itemId, [FromBody] CustomDictionaryItemRequest request) =>
        await Run(() => _dictionaries.UpdateItemAsync(itemId, request));

    [HttpDelete("dictionaries/items/{itemId:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    public async Task<IActionResult> DeleteItem(int itemId) =>
        await Run(() => _dictionaries.DeleteItemAsync(itemId));

    // ── Представления журналов (GEN-10) ──────────────────────────────────────

    /// <summary>Представления журнала: общие банка и личные текущего сотрудника.</summary>
    [HttpGet("views")]
    public async Task<IActionResult> Views([FromQuery] string scope) =>
        await Run(() => _views.ListAsync(scope, _currentUser.UserId));

    [HttpPost("views")]
    public async Task<IActionResult> SaveView([FromBody] ListViewSaveRequest request) =>
        await Run(() => _views.SaveAsync(request, _currentUser.UserId));

    [HttpDelete("views/{id:int}")]
    public async Task<IActionResult> DeleteView(int id) =>
        await Run(async () => { await _views.DeleteAsync(id, _currentUser.UserId); return true; });

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new {message = ex.Message});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
