using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Correspondence.Models;

namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Разовая инициализация нумераторов при переходе контуров на INumeratorService.
///
/// NumeratorService при первом обращении заводит нумератор с NextSeq = 1. Для контуров,
/// которые уже вели собственную нумерацию (ВНД, Кадры, конкурс/протокол, Доверенности,
/// Корреспонденция, Заседания), это означало бы выдачу номера, уже занятого живым
/// документом. Поэтому перед первым NextAsync разово вписываем текущий максимум:
/// NextSeq = (максимальный существующий номер в группе) + 1.
///
/// Идемпотентно: строку нумератора трогаем только если её ещё нет. После первого запуска
/// нумераторы ведёт NumeratorService, и повторный сидинг ничего не перезаписывает. Новый
/// год/орган/направление появляются уже через NumeratorService (NextSeq = 1) — сброс
/// нумерации в новом периоде и есть ожидаемое поведение.
/// </summary>
public static class NumeratorSeeder
{
    public static async Task SeedAsync(DelosferaDbContext db)
    {
        var existing = (await db.Numerators
                .Select(n => new { n.DocumentType, n.Scope, n.ScopeKey })
                .ToListAsync())
            .Select(n => (n.DocumentType, n.Scope, n.ScopeKey))
            .ToHashSet();

        var toAdd = new List<Numerator>();

        void Seed(DocumentType type, string scope, string scopeKey, string pattern, int nextSeq)
        {
            if (nextSeq < 1) nextSeq = 1;
            if (existing.Contains((type, scope, scopeKey))) return;
            toAdd.Add(new Numerator
            {
                DocumentType = type, Scope = scope, ScopeKey = scopeKey, Pattern = pattern, NextSeq = nextSeq,
            });
            existing.Add((type, scope, scopeKey));
        }

        // Ведущие цифры строки («12-лс» → 12, «исх-7/2026» → 7). Та же логика, что в
        // заменяемых NextNumberAsync соответствующих контуров.
        static int LeadingDigits(string s)
        {
            var digits = new string(s.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var v) ? v : 0;
        }

        // Цифры после префикса фиксированной длины («КНК-2026-0005» → 5).
        static int AfterPrefix(string s, string prefix) =>
            s.StartsWith(prefix) && int.TryParse(s[prefix.Length..], out var v) ? v : 0;

        // --- ВНД: единый сквозной код, стартовое значение 10210 (VndService.GenerateNextCodeAsync).
        var vndMax = (await db.VndDocuments.Select(x => x.Code).ToListAsync())
            .Select(c => int.TryParse(c, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        Seed(DocumentType.Vnd, "code", "global", "{seq}", Math.Max(vndMax, 10209) + 1);

        // --- Кадры: «{n}-лс» по году (HrOrderController.NextNumberAsync).
        var hr = await db.HrOrders
            .Where(o => o.RegNumber != null)
            .Select(o => new { o.Year, o.RegNumber })
            .ToListAsync();
        foreach (var g in hr.GroupBy(o => o.Year))
            Seed(DocumentType.HrOrder, "global", g.Key.ToString(), "{seq}-лс",
                g.Max(o => LeadingDigits(o.RegNumber!)) + 1);

        // --- Закупки: конкурс «КНК-{year}-{seq:D4}» и протокол «ПЗ-{year}-{seq:D4}» по году.
        var tenders = await db.Tenders.Where(t => t.RegNumber != null)
            .Select(t => t.RegNumber!).ToListAsync();
        foreach (var g in tenders.GroupBy(YearOf))
        {
            var prefix = $"КНК-{g.Key}-";
            Seed(DocumentType.Procurement, "tender", g.Key.ToString(), $"КНК-{{year}}-{{seq:D4}}",
                g.Max(n => AfterPrefix(n, prefix)) + 1);
        }

        var protocols = await db.ProcurementProtocols.Where(p => p.RegNumber != null)
            .Select(p => p.RegNumber!).ToListAsync();
        foreach (var g in protocols.GroupBy(YearOf))
        {
            var prefix = $"ПЗ-{g.Key}-";
            Seed(DocumentType.Procurement, "protocol", g.Key.ToString(), $"ПЗ-{{year}}-{{seq:D4}}",
                g.Max(n => AfterPrefix(n, prefix)) + 1);
        }

        // --- Доверенности: «{n}/{year}» по году (PoaService.NextNumberAsync).
        var poa = await db.PowersOfAttorney
            .Where(p => p.RegNumber != null)
            .Select(p => new { p.Year, p.RegNumber })
            .ToListAsync();
        foreach (var g in poa.GroupBy(p => p.Year))
            Seed(DocumentType.PowerOfAttorney, "global", g.Key.ToString(), "{seq}/{year}",
                g.Max(p => LeadingDigits(p.RegNumber!)) + 1);

        // --- Корреспонденция: «вх-{n}/{year}» / «исх-{n}/{year}» по направлению и году.
        var letters = await db.CorrespondenceLetters
            .Where(l => l.RegNumber != null)
            .Select(l => new { l.Direction, l.Year, l.RegNumber })
            .ToListAsync();
        foreach (var g in letters.GroupBy(l => new { l.Direction, l.Year }))
        {
            var prefix = g.Key.Direction == LetterDirection.Incoming ? "вх" : "исх";
            Seed(DocumentType.Correspondence, g.Key.Direction.ToString(), g.Key.Year.ToString(),
                $"{prefix}-{{seq}}/{{year}}", g.Max(l => LeadingDigits(l.RegNumber!)) + 1);
        }

        // --- Заседания: сквозной int-номер по органу и году (MeetingService.NextNumberAsync).
        var meetings = await db.Meetings
            .Select(m => new { m.Body, m.Year, m.Number })
            .ToListAsync();
        foreach (var g in meetings.GroupBy(m => new { m.Body, m.Year }))
            Seed(DocumentType.Meeting, g.Key.Body.ToString(), g.Key.Year.ToString(), "{seq}",
                g.Max(m => m.Number) + 1);

        if (toAdd.Count > 0)
        {
            db.Numerators.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private static int YearOf(string regNumber)
    {
        // «КНК-2026-0005» / «ПЗ-2026-0005» — год во втором сегменте.
        var parts = regNumber.Split('-');
        return parts.Length >= 2 && int.TryParse(parts[1], out var y) ? y : 0;
    }
}
