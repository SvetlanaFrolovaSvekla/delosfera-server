using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;

namespace delosfera_server.Modules.Meetings.Services;

/// <summary>Сборка DTO вопроса повестки — одинаковая в карточке заседания и в отдельных ответах.</summary>
public static class MeetingMapper
{
    public static AgendaItemDto ToDto(AgendaItem item, DateOnly today, bool canManage) => new()
    {
        Id = item.Id,
        MeetingId = item.MeetingId,
        Order = item.Order,
        Topic = item.Topic,
        ProtocolNumber = item.ProtocolNumber,
        ProtocolDate = item.ProtocolDate,
        Decision = item.Decision,

        SpeakerUserId = item.SpeakerUserId,
        SpeakerName = item.Speaker?.FullName,
        SpeakerHeadUserId = item.SpeakerHeadUserId,
        SpeakerHeadName = item.SpeakerHead?.FullName,
        SpeakerUnitId = item.SpeakerUnitId,
        SpeakerUnitTitle = item.SpeakerUnit?.TitleRu,
        DeputySecretaryUserId = item.DeputySecretaryUserId,
        DeputySecretaryName = item.DeputySecretary?.FullName,
        ControllerUserId = item.ControllerUserId,
        ControllerName = item.Controller?.FullName,

        DocumentsUrl = item.DocumentsUrl,

        Guests = item.Guests.Select(g => new AgendaGuestDto
        {
            Id = g.Id,
            UserId = g.UserId,
            UserName = g.User?.FullName ?? string.Empty,
            OrgUnitId = g.OrgUnitId,
            OrgUnitTitle = g.OrgUnit?.TitleRu,
        }).ToList(),

        Assignments = item.Assignments
            .OrderBy(a => a.DueDate ?? DateOnly.MaxValue)
            .Select(a => ToDto(a, today))
            .ToList(),

        Files = item.Files.Select(f => new AgendaFileDto
        {
            Id = f.Id,
            Kind = f.Kind,
            KindTitle = MeetingTitles.FileKind(f.Kind),
            FileId = f.FileId,
            FileName = f.File?.OriginalFileName ?? string.Empty,
            SizeBytes = f.File?.SizeBytes ?? 0,
            CreatedAt = f.CreatedAt,
            // Файлы удаляет только секретарь: приложенные материалы — часть протокола.
            CanDelete = canManage,
        }).ToList(),
    };

    public static AgendaAssignmentDto ToDto(AgendaAssignment a, DateOnly today) => new()
    {
        Id = a.Id,
        AgendaItemId = a.AgendaItemId,
        UserId = a.UserId,
        UserName = a.User?.FullName ?? string.Empty,
        OrgUnitId = a.OrgUnitId,
        OrgUnitTitle = a.OrgUnit?.TitleRu,
        Text = a.Text,
        DueDate = a.DueDate,
        Status = a.Status,
        StatusTitle = MeetingTitles.Status(a.Status),
        Report = a.Report,
        ReportedAt = a.ReportedAt,
        IsOverdue = a.DueDate is { } due && due < today && MeetingTitles.IsOpen(a.Status),
        DaysLeft = a.DueDate is { } d ? d.DayNumber - today.DayNumber : null,
    };
}
