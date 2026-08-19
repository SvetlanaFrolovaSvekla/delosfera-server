using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IPublicationService
{
    Task<PublicationPackageDto> BuildAsync(int tenderId);
}

/// <summary>
/// Пакет публикации объявления о конкурсе (INT-05).
///
/// Автоматическая отправка на сайт Банка и tenders.kg отложена: у площадок нет
/// согласованного API, а ТЗ помечает интеграцию как желательную. Поэтому система
/// собирает готовый к публикации текст и реквизиты — их выкладывают вручную, но
/// содержание формируется из карточки конкурса, а не переписывается заново.
/// </summary>
public class PublicationService : IPublicationService
{
    private const string BankTitle = "ОАО «Керемет Банк»";
    private const string BankAddress = "г. Бишкек, пр. Чуй 219";
    private const string BankInn = "02508201310035";

    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public PublicationService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PublicationPackageDto> BuildAsync(int tenderId)
    {
        var tender = await _db.Tenders
            .Include(t => t.Request).ThenInclude(r => r!.Method)
            .Include(t => t.Request).ThenInclude(r => r!.InitiatorUnit)
            .FirstOrDefaultAsync(t => t.Id == tenderId)
            ?? throw new KeyNotFoundException("Конкурс не найден");

        var request = tender.Request!;

        var dto = new PublicationPackageDto
        {
            TenderId = tender.Id,
            TenderRegNumber = tender.RegNumber,
            Subject = request.Subject,
            MethodTitle = request.Method!.TitleRu,
            Amount = request.Amount,
            InitiatorUnit = request.InitiatorUnit?.TitleRu,
            PublishedOn = tender.PublishedOn,
            SubmissionDeadline = tender.SubmissionDeadline,
            IsLimited = tender.IsLimited,
            Announcement = BuildAnnouncement(tender, request),
        };

        // Конкурс с ограниченным участием не публикуется: приглашения рассылаются
        // определённому кругу поставщиков, и факт рассылки фиксируется в системе.
        dto.Channels = tender.IsLimited
            ? ["Приглашения участникам (публикация не требуется)"]
            : ["Сайт ОАО «Керемет Банк»", "tenders.kg"];

        if (tender.Status == TenderStatus.Draft)
            dto.Blockers.Add("Конкурс не объявлен — пакет публикации формируется после объявления");

        if (tender.SubmissionDeadline is null)
            dto.Blockers.Add("Не задан окончательный срок приёма заявок");

        return dto;
    }

    private string BuildAnnouncement(Tender tender, ProcurementRequest request)
    {
        var deadline = tender.SubmissionDeadline?.ToString("dd.MM.yyyy") ?? "___";
        var published = (tender.PublishedOn ?? _clock.Today).ToString("dd.MM.yyyy");

        return $"""
                {BankTitle} объявляет конкурс на закупку: {request.Subject}.

                Способ закупки: {request.Method!.TitleRu}.
                Ориентировочная сумма закупки: {request.Amount:### ### ### ##0.##} сом.
                Инициирующее подразделение: {request.InitiatorUnit?.TitleRu ?? "—"}.

                Конкурсные заявки принимаются до {deadline} включительно.
                Заявки, поступившие после указанного срока, к вскрытию не принимаются.

                Номер конкурса: {tender.RegNumber}. Дата объявления: {published}.

                Адрес: {BankAddress}. ИНН: {BankInn}.
                Контакты и конкурсная документация предоставляются по запросу
                в Сектор закупок Административного отдела.
                """;
    }
}
