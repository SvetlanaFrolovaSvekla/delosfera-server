using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Протокол на каждое заседание комиссии.
///
/// «Протокол составляется на каждое заседание комиссии по закупке» — Положение
/// повторяет это трижды. Комиссия собирается не один раз: вскрытие заявок, их
/// изучение в течение десяти рабочих дней, определение победителя. Пока протокол
/// был один на заявку, каждое следующее заседание затирало предыдущее, и ход
/// обсуждения восстановить было нельзя.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProtocolPerMeetingTests
{
    private readonly PostgresFixture _postgres;

    public ProtocolPerMeetingTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Новое_заседание_даёт_новый_протокол()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 22));
        var первый = await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 29));
        var второй = await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        Assert.NotEqual(первый.Id, второй.Id);
        Assert.Equal(new DateOnly(2026, 9, 22), первый.MeetingDate);
        Assert.Equal(new DateOnly(2026, 9, 29), второй.MeetingDate);
    }

    [Fact]
    public async Task Повторная_сборка_того_же_заседания_протокол_не_плодит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 22));

        var первый = await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);
        var второй = await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        // Пересборка исправляет протокол того же заседания, а не заводит второй:
        // иначе одно заседание получало бы по протоколу на каждое нажатие.
        Assert.Equal(первый.Id, второй.Id);
        Assert.Single(await сервис.ListAsync(стенд.RequestId));
    }

    [Fact]
    public async Task Прежние_протоколы_остаются_читаемыми()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 22));
        await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 29));
        await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        var все = await сервис.ListAsync(стенд.RequestId);

        Assert.Equal(2, все.Count);
        // Свежие сверху: обычно нужен последний, прежние читают ради хода дела.
        Assert.Equal(new DateOnly(2026, 9, 29), все[0].MeetingDate);
        Assert.Equal(new DateOnly(2026, 9, 22), все[1].MeetingDate);
    }

    [Fact]
    public async Task Протокол_закупки_без_уточнения_это_последний()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 22));
        await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        await НазначитьЗаседаниеАsync(db, стенд.TenderId, new DateOnly(2026, 9, 29));
        var последний = await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        // Его подписывают и по нему заключают договор.
        var текущий = await сервис.GetAsync(стенд.RequestId);

        Assert.Equal(последний.Id, текущий!.Id);
    }

    [Fact]
    public async Task Без_конкурса_протокол_остаётся_один()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сКонкурсом: false);
        var сервис = Сервис(db);

        await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);
        await сервис.GenerateAsync(стенд.RequestId, стенд.Actor);

        // У простой закупки комиссия не создаётся, заседаний нет — и делить
        // протокол не по чему.
        var все = await сервис.ListAsync(стенд.RequestId);

        Assert.Single(все);
        Assert.Null(все[0].MeetingDate);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int RequestId, int TenderId, int Actor);

    private static IProtocolService Сервис(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var clock = new delosfera_server.Common.Services.BankClock();

        return new ProtocolService(db, new ProposalService(db, audit, clock), audit, clock);
    }

    private static async Task НазначитьЗаседаниеАsync(
        DelosferaDbContext db, int tenderId, DateOnly date)
    {
        var tender = await db.Tenders.FirstAsync(t => t.Id == tenderId);
        tender.MeetingDate = date;
        await db.SaveChangesAsync();
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db, bool сКонкурсом = true)
    {
        var автор = new User
        {
            FullName = "Организатор закупки",
            Email = $"proto-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(автор);

        var поставщик = new Supplier {Title = "ОсОО Победитель", Inn = "01234567890123"};
        var второй = new Supplier {Title = "ОсОО Второй", Inn = "01234567890124"};
        db.Suppliers.AddRange(поставщик, второй);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = "OnApproval",
            AuthorId = автор.Id,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Серверное оборудование",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 5_000_000m,
            MethodId = method.Id,
            ProtocolRequired = true,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var tenderId = 0;

        if (сКонкурсом)
        {
            var tender = new Tender {RequestId = request.Id, Status = TenderStatus.Decided};
            db.Tenders.Add(tender);
            await db.SaveChangesAsync();

            db.TenderBids.AddRange(
                new TenderBid
                {
                    TenderId = tender.Id, SupplierId = поставщик.Id,
                    Price = 4_800_000m, IsAdmitted = true, IsWinner = true,
                },
                new TenderBid
                {
                    TenderId = tender.Id, SupplierId = второй.Id,
                    Price = 4_950_000m, IsAdmitted = true,
                });

            await db.SaveChangesAsync();
            tenderId = tender.Id;
        }
        else
        {
            db.CommercialProposals.Add(new CommercialProposal
            {
                RequestId = request.Id,
                SupplierId = поставщик.Id,
                Price = 40_000m,
                MeetsRequirements = true,
                IsWinner = true,
            });

            await db.SaveChangesAsync();
        }

        return new Стенд(request.Id, tenderId, автор.Id);
    }
}
