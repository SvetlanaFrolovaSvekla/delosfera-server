using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;

namespace delosfera_server.Modules.Meetings.Services;

/// <summary>
/// Где документ рассматривался на коллегиальном органе.
///
/// Записка помнит, что её вынесли на орган; вопрос повестки помнит, из какой
/// записки или заявки он вырос. Связь была, а в карточке её не показывали:
/// человек видел документ и не знал, дошёл ли тот до Правления и чем кончилось.
///
/// Поиск один на оба контура: вопрос устроен одинаково, откуда бы он ни пришёл.
/// </summary>
public static class BoardReviewLookup
{
    /// <summary>Рассмотрение записки; null — вопрос в повестку не включён.</summary>
    public static Task<BoardReviewDto?> ForSzAsync(
        DelosferaDbContext db, int szId, CancellationToken ct = default) =>
        FindAsync(db, i => i.SourceSzId == szId, ct);

    /// <summary>Рассмотрение заявки на закупку; null — на орган не выносилась.</summary>
    public static Task<BoardReviewDto?> ForProcurementAsync(
        DelosferaDbContext db, int requestId, CancellationToken ct = default) =>
        FindAsync(db, i => i.SourceProcurementRequestId == requestId, ct);

    private static async Task<BoardReviewDto?> FindAsync(
        DelosferaDbContext db,
        System.Linq.Expressions.Expression<Func<Models.AgendaItem, bool>> where,
        CancellationToken ct)
    {
        return await db.AgendaItems.AsNoTracking()
            .Where(where)
            // Документ могли выносить не один раз — показываем последнее заседание.
            .OrderByDescending(i => i.Meeting!.Date)
            .ThenByDescending(i => i.Id)
            .Select(i => new BoardReviewDto
            {
                MeetingId = i.MeetingId,
                BodyTitle = MeetingTitles.Body(i.Meeting!.Body),
                MeetingDate = i.Meeting.Date,
                AgendaItemId = i.Id,
                Order = i.Order,
                Topic = i.Topic,
                DraftResolution = i.DraftResolution,
                Decision = i.Decision,
                ProtocolNumber = i.ProtocolNumber,
                ProtocolDate = i.ProtocolDate,
            })
            .FirstOrDefaultAsync(ct);
    }
}
