using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Куда документ ушёл на коллегиальный орган.
///
/// Записка помнит, что её вынесли на орган; вопрос повестки помнит, из какой
/// записки или заявки он вырос. Связь существовала, а в карточке её не
/// показывали: автор видел свою записку и не знал, дошла ли она до Правления и
/// чем там кончилось.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BoardReviewLinkTests
{
    private readonly PostgresFixture _postgres;

    public BoardReviewLinkTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task По_записке_видно_заседание_и_номер_вопроса()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var рассмотрение = await BoardReviewLookup.ForSzAsync(db, стенд.SzId);

        Assert.NotNull(рассмотрение);
        Assert.Equal("Правление", рассмотрение!.BodyTitle);
        Assert.Equal(2, рассмотрение.Order);
        Assert.Equal("Закупка мониторов", рассмотрение.Topic);
    }

    [Fact]
    public async Task Решение_видно_после_заседания()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await db.AgendaItems.Where(i => i.Id == стенд.AgendaItemId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Decision, "Утвердить закупку в пределах сметы")
                .SetProperty(i => i.ProtocolNumber, "49(14)"));

        var рассмотрение = await BoardReviewLookup.ForSzAsync(db, стенд.SzId);

        Assert.Equal("Утвердить закупку в пределах сметы", рассмотрение!.Decision);
        Assert.Equal("49(14)", рассмотрение.ProtocolNumber);
    }

    [Fact]
    public async Task До_заседания_виден_проект_постановления_без_решения()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var рассмотрение = await BoardReviewLookup.ForSzAsync(db, стенд.SzId);

        // Проект готовится заранее и рассылается с материалами; решение появится
        // после заседания и может отличаться.
        Assert.Equal("Проект: одобрить", рассмотрение!.DraftResolution);
        Assert.Null(рассмотрение.Decision);
    }

    [Fact]
    public async Task По_заявке_на_закупку_рассмотрение_тоже_находится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var рассмотрение = await BoardReviewLookup.ForProcurementAsync(db, стенд.RequestId);

        Assert.NotNull(рассмотрение);
        Assert.Equal(стенд.AgendaItemId, рассмотрение!.AgendaItemId);
    }

    [Fact]
    public async Task Документ_не_выносившийся_на_орган_рассмотрения_не_имеет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        Assert.Null(await BoardReviewLookup.ForSzAsync(db, стенд.SzБезПовестки));
    }

    [Fact]
    public async Task Показывается_последнее_заседание_если_выносили_дважды()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Вопрос сняли и вынесли повторно — человека интересует последнее.
        var позже = new Meeting
        {
            Body = MeetingBody.Board,
            Form = MeetingForm.InPerson,
            Date = new DateOnly(2026, 12, 1),
            Time = new TimeOnly(10, 0),
            Year = 2026,
            Number = 2,
            SecretaryUserId = стенд.Secretary,
        };
        db.Meetings.Add(позже);
        await db.SaveChangesAsync();

        db.AgendaItems.Add(new AgendaItem
        {
            MeetingId = позже.Id,
            Order = 1,
            Topic = "Закупка мониторов (повторно)",
            SourceSzId = стенд.SzId,
        });
        await db.SaveChangesAsync();

        var рассмотрение = await BoardReviewLookup.ForSzAsync(db, стенд.SzId);

        Assert.Equal(new DateOnly(2026, 12, 1), рассмотрение!.MeetingDate);
        Assert.Equal("Закупка мониторов (повторно)", рассмотрение.Topic);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int SzId, int SzБезПовестки, int RequestId, int AgendaItemId, int Secretary);

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var секретарь = await ПользовательАsync(db, "Секретарь Правления");
        var автор = await ПользовательАsync(db, "Автор записки");

        var kind = await db.SzKinds.AsNoTracking().FirstAsync();
        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var szDoc = new Document
        {
            Type = DocumentType.Sz, Title = "О закупке мониторов",
            StatusCode = "Registered", AuthorId = автор,
        };
        var szDoc2 = new Document
        {
            Type = DocumentType.Sz, Title = "Записка без повестки",
            StatusCode = "Registered", AuthorId = автор,
        };
        var reqDoc = new Document
        {
            Type = DocumentType.Procurement, Title = "Заявка на мониторы",
            StatusCode = "OnApproval", AuthorId = автор,
        };
        db.Documents.AddRange(szDoc, szDoc2, reqDoc);
        await db.SaveChangesAsync();

        var sz = new delosfera_server.Modules.Sz.Models.SzDocument
        {
            DocumentId = szDoc.Id, KindId = kind.Id, Body = "Текст",
        };
        var sz2 = new delosfera_server.Modules.Sz.Models.SzDocument
        {
            DocumentId = szDoc2.Id, KindId = kind.Id, Body = "Текст",
        };
        var request = new delosfera_server.Modules.Procurement.Models.ProcurementRequest
        {
            DocumentId = reqDoc.Id, Subject = "Мониторы", Amount = 45_000m, MethodId = method.Id,
        };
        db.AddRange(sz, sz2, request);

        var meeting = new Meeting
        {
            Body = MeetingBody.Board,
            Form = MeetingForm.InPerson,
            Date = new DateOnly(2026, 9, 20),
            Time = new TimeOnly(10, 0),
            Year = 2026,
            Number = 1,
            SecretaryUserId = секретарь,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var item = new AgendaItem
        {
            MeetingId = meeting.Id,
            Order = 2,
            Topic = "Закупка мониторов",
            DraftResolution = "Проект: одобрить",
            SourceSzId = sz.Id,
            SourceProcurementRequestId = request.Id,
        };
        db.AgendaItems.Add(item);
        await db.SaveChangesAsync();

        return new Стенд(sz.Id, sz2.Id, request.Id, item.Id, секретарь);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"link-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
