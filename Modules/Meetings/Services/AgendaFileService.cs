using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Modules.Meetings.Services;

public interface IAgendaFileService
{
    Task<AgendaFileDto> AttachAsync(int itemId, MeetingFileKind kind, IFormFile file, int currentUserId);
    Task DetachAsync(int agendaFileId);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int agendaFileId);
}

/// <summary>
/// Файлы вопроса повестки: служебная записка, протокол и документы об исполнении.
///
/// Прикладывать протокол и СЗ может только секретарь — это материалы заседания.
/// Файлы об исполнении грузит исполнитель: по ТЗ они доступны всем сотрудникам,
/// потому что подтверждают закрытие поручения.
///
/// Удаление файлов оставлено секретарю: приложенное к протоколу не должно исчезать
/// по решению исполнителя.
/// </summary>
public class AgendaFileService : IAgendaFileService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IMeetingAccessService _access;

    public AgendaFileService(
        DelosferaDbContext db,
        IFileStorageService files,
        IMeetingAccessService access)
    {
        _db = db;
        _files = files;
        _access = access;
    }

    public async Task<AgendaFileDto> AttachAsync(
        int itemId, MeetingFileKind kind, IFormFile file, int currentUserId)
    {
        var item = await _db.AgendaItems
            .Include(i => i.Meeting)
            .Include(i => i.Assignments)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Вопрос повестки не найден");

        var body = item.Meeting!.Body;

        if (kind == MeetingFileKind.Execution)
        {
            var isExecutor = item.Assignments.Any(a => a.UserId == currentUserId);
            if (!isExecutor && !_access.CanReport && !_access.CanManage(body))
                throw new UnauthorizedAccessException(
                    "Файл об исполнении прикладывает ответственный или секретарь");
        }
        else
        {
            _access.RequireManage(body);
        }

        var stored = await _files.SaveAsync(file, currentUserId);

        var link = new AgendaFile
        {
            AgendaItemId = itemId,
            Kind = kind,
            FileId = stored.Id,
            UploadedByUserId = currentUserId,
        };

        _db.AgendaFiles.Add(link);
        await _db.SaveChangesAsync();

        return new AgendaFileDto
        {
            Id = link.Id,
            Kind = kind,
            KindTitle = MeetingTitles.FileKind(kind),
            FileId = stored.Id,
            FileName = stored.OriginalFileName,
            SizeBytes = stored.SizeBytes,
            CreatedAt = link.CreatedAt,
            CanDelete = _access.CanManage(body),
        };
    }

    public async Task DetachAsync(int agendaFileId)
    {
        var link = await Load(agendaFileId);
        _access.RequireManage(link.AgendaItem!.Meeting!.Body);

        var fileId = link.FileId;
        _db.AgendaFiles.Remove(link);
        await _db.SaveChangesAsync();

        await _files.DeleteAsync(fileId);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int agendaFileId)
    {
        var link = await Load(agendaFileId);
        var meetingId = link.AgendaItem!.MeetingId;

        // Файл об исполнении открыт всем сотрудникам; остальные материалы — только тем,
        // кому доступен сам вопрос повестки.
        if (link.Kind != MeetingFileKind.Execution)
        {
            var visible = await _access.VisibleItemIdsAsync(meetingId);
            if (!visible.Contains(link.AgendaItemId))
                throw new UnauthorizedAccessException("Материалы этого вопроса повестки недоступны");
        }

        return await _files.DownloadAsync(link.FileId);
    }

    private async Task<AgendaFile> Load(int agendaFileId) =>
        await _db.AgendaFiles
            .Include(f => f.AgendaItem).ThenInclude(i => i!.Meeting)
            .FirstOrDefaultAsync(f => f.Id == agendaFileId)
        ?? throw new KeyNotFoundException("Файл не найден");
}
