using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Индексирует легаси-гиперссылки db://documents/{код} (старая система isrib), прошитые
/// прямо в Word-файлы редакций, в таблицу vnd_link (Kind = LegacyText) - по одной строке на
/// (редакция, язык, код документа).
///
/// Зачем хранить, а не вычислять на лету, как раньше (раньше ссылки извлекались только из
/// текущей редакции и только при открытии вкладки "Связи" ССЫЛАЮЩЕГОСЯ документа):
/// - у документа-ЦЕЛИ такие ссылки не появлялись в "Ссылающихся документах" вовсе (чтобы их
///   найти, пришлось бы при каждом открытии скачивать и разбирать Word-файлы всех документов);
/// - нельзя было показать, КАКАЯ редакция ссылается (Р1 ссылается на это, Р2 на это и т.д.).
///
/// Каждая редакция помнит "отпечаток" файлов, для которых индекс уже построен
/// (VndRedaction.LegacyLinksScanKey) - переиндексируется только то, что поменялось. Индекс
/// обновляется (1) при открытии вкладки "Связанные документы" - для редакций этого документа,
/// (2) фоновым воркером VndLegacyLinkIndexWorker - для всех остальных.</summary>
public interface IVndLegacyLinkIndexer
{
    /// <summary>Переиндексировать (если нужно) все редакции одного документа.</summary>
    Task IndexVndAsync(int vndId, CancellationToken ct = default);

    /// <summary>Переиндексировать до batchSize редакций, у которых индекс устарел или ещё не
    /// строился. Возвращает, сколько редакций было обработано (0 - всё актуально).</summary>
    Task<int> IndexPendingAsync(int batchSize, CancellationToken ct = default);
}

public class VndLegacyLinkIndexer : IVndLegacyLinkIndexer
{
    /// <summary>Версия алгоритма - входит в отпечаток, чтобы при изменении логики извлечения
    /// всё переиндексировалось само.</summary>
    private const string ScanKeyVersion = "v2"; // v2 - плюс номера вложений (LegacyAttachmentRefs)

    /// <summary>Отметка "индексация упала" (например, файл недоступен в хранилище) - такая
    /// редакция повторяется, но в последнюю очередь (см. IndexPendingAsync).</summary>
    private const string FailedScanKey = "failed";

    // Один процесс - одна индексация за раз: иначе параллельные открытия вкладки "Связи" одного
    // и того же документа (или открытие + фоновый воркер) могли бы задвоить строки.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly IDocxLegacyLinkExtractor _extractor;
    private readonly ILogger _logger;

    public VndLegacyLinkIndexer(
        DelosferaDbContext db, IFileStorageService fileService, IDocxLegacyLinkExtractor extractor,
        ILogger<VndLegacyLinkIndexer> logger)
        : this(db, fileService, extractor, (ILogger)logger)
    {
    }

    /// <summary>Для использования изнутри других сервисов (см. VndService.GetLinksAsync) без
    /// изменения их конструкторов. internal - чтобы DI видел ровно один публичный конструктор.</summary>
    internal VndLegacyLinkIndexer(
        DelosferaDbContext db, IFileStorageService fileService, IDocxLegacyLinkExtractor extractor, ILogger logger)
    {
        _db = db;
        _fileService = fileService;
        _extractor = extractor;
        _logger = logger;
    }

    public static string BuildScanKey(VndRedaction r) =>
        string.Join('|',
            ScanKeyVersion,
            r.DocFileRuId,
            r.DocFileKgId?.ToString() ?? "-",
            r.DocFileEnId?.ToString() ?? "-",
            r.DocRuUpdatedAt?.Ticks.ToString() ?? "-",
            r.DocKgUpdatedAt?.Ticks.ToString() ?? "-",
            r.DocEnUpdatedAt?.Ticks.ToString() ?? "-");

    public async Task IndexVndAsync(int vndId, CancellationToken ct = default)
    {
        var redactions = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .ToListAsync(ct);

        var stale = redactions.Where(r => r.LegacyLinksScanKey != BuildScanKey(r)).ToList();
        if (stale.Count == 0) return;

        await IndexRedactionsAsync(stale, ct);
    }

    public async Task<int> IndexPendingAsync(int batchSize, CancellationToken ct = default)
    {
        // Отпечаток считается в C# (в нём несколько полей и форматирование) - поэтому тянем только
        // нужные для него колонки, а сами сущности загружаем уже для небольшой пачки.
        var candidates = await _db.VndRedactions
            .AsNoTracking()
            .Select(r => new
            {
                r.Id, r.DocFileRuId, r.DocFileKgId, r.DocFileEnId,
                r.DocRuUpdatedAt, r.DocKgUpdatedAt, r.DocEnUpdatedAt, r.LegacyLinksScanKey,
            })
            .ToListAsync(ct);

        var staleIds = candidates
            .Where(c => c.LegacyLinksScanKey != string.Join('|',
                ScanKeyVersion,
                c.DocFileRuId,
                c.DocFileKgId?.ToString() ?? "-",
                c.DocFileEnId?.ToString() ?? "-",
                c.DocRuUpdatedAt?.Ticks.ToString() ?? "-",
                c.DocKgUpdatedAt?.Ticks.ToString() ?? "-",
                c.DocEnUpdatedAt?.Ticks.ToString() ?? "-"))
            // Сначала - ещё ни разу не индексированные/изменившиеся, и только потом - те, на
            // которых индексация в прошлый раз упала (FailedScanKey): иначе пачка стабильно
            // "битых" редакций навсегда загородила бы собой все остальные.
            .OrderBy(c => c.LegacyLinksScanKey == FailedScanKey ? 1 : 0)
            .ThenBy(c => c.Id)
            .Select(c => c.Id)
            .Take(batchSize)
            .ToList();

        if (staleIds.Count == 0) return 0;

        var redactions = await _db.VndRedactions
            .Where(r => staleIds.Contains(r.Id))
            .ToListAsync(ct);

        return await IndexRedactionsAsync(redactions, ct);
    }

    /// <summary>Возвращает число успешно проиндексированных редакций.</summary>
    private async Task<int> IndexRedactionsAsync(List<VndRedaction> redactions, CancellationToken ct)
    {
        var succeeded = 0;
        await Gate.WaitAsync(ct);
        try
        {
            foreach (var redaction in redactions)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await IndexRedactionAsync(redaction, ct);
                    succeeded++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Одна проблемная редакция не должна ронять ни показ "Связей", ни весь проход
                    // воркера - отпечаток не обновляется, попробуем в следующий раз.
                    _logger.LogWarning(ex, "Не удалось проиндексировать легаси-ссылки редакции {RedactionId}", redaction.Id);
                    _db.ChangeTracker.Clear();
                    try
                    {
                        await SetScanKeyAsync(redaction.Id, FailedScanKey, CancellationToken.None);
                    }
                    catch (Exception markEx)
                    {
                        _logger.LogWarning(markEx, "Не удалось отметить редакцию {RedactionId} как непроиндексированную", redaction.Id);
                    }
                }
            }
        }
        finally
        {
            Gate.Release();
        }

        return succeeded;
    }

    /// <summary>Отпечаток пишется напрямую (ExecuteUpdate), мимо SaveChanges - чтобы служебная
    /// отметка индекса не считалась правкой самой редакции (UpdatedAt и аудит не трогаются).</summary>
    private Task SetScanKeyAsync(int redactionId, string key, CancellationToken ct) =>
        _db.VndRedactions
            .Where(r => r.Id == redactionId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LegacyLinksScanKey, key), ct);

    private async Task IndexRedactionAsync(VndRedaction redaction, CancellationToken ct)
    {
        var key = BuildScanKey(redaction);

        // Файл уже мог смениться, пока мы ждали Gate - перечитываем актуальный отпечаток.
        var stored = await _db.VndRedactions
            .Where(r => r.Id == redaction.Id)
            .Select(r => r.LegacyLinksScanKey)
            .FirstOrDefaultAsync(ct);
        if (stored == key) return;

        var files = new List<(string Lang, int FileId)> { ("ru", redaction.DocFileRuId) };
        if (redaction.DocFileKgId is { } kg) files.Add(("kg", kg));
        if (redaction.DocFileEnId is { } en) files.Add(("en", en));

        var found = new List<(string Lang, string Code)>();
        var attachmentRefs = new List<string>();
        foreach (var (lang, fileId) in files)
        {
            if (fileId <= 0) continue;
            // Ошибку скачивания НЕ глотаем (в отличие от разбора самого файла - см.
            // DocxLegacyLinkExtractor): иначе недоступное хранилище "обнулило" бы индекс.
            var (stream, _, _) = await _fileService.DownloadAsync(fileId);
            await using (stream)
            {
                var refs = _extractor.Extract(stream);
                found.AddRange(refs.DocumentCodes.Select(code => (lang, code)));
                if (refs.AttachmentIndexes.Count > 0)
                    attachmentRefs.Add($"{lang}:{string.Join(',', refs.AttachmentIndexes)}");
            }
        }

        var codes = found.Select(f => f.Code).Distinct().ToList();
        var targetsByCode = codes.Count == 0
            ? new Dictionary<string, int>()
            : (await _db.VndDocuments
                .Where(d => codes.Contains(d.Code) && d.Id != redaction.VndId)
                .Select(d => new { d.Id, d.Code })
                .ToListAsync(ct))
            .GroupBy(d => d.Code)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Уже проиндексированные строки, которые остались в тексте, НЕ пересоздаём, а оставляем
        // как есть - чтобы не менялись их id (по id строятся адреса "показать ссылку в тексте",
        // ?link=..., которые могли сохранить или переслать).
        var existing = await _db.VndLinks
            .Where(l => l.SourceRedactionId == redaction.Id && l.Kind == VndLinkKind.LegacyText)
            .ToListAsync(ct);

        var desired = found.Distinct()
            .Where(f => targetsByCode.ContainsKey(f.Code)) // документа с таким кодом нет - пропускаем
            .Select(f => (f.Lang, f.Code, TargetId: targetsByCode[f.Code]))
            .ToList();

        foreach (var link in existing)
        {
            var keep = desired.FindIndex(d =>
                d.Lang == link.SourceDocumentTarget && d.Code == link.LegacyCode && d.TargetId == link.TargetVndId);
            if (keep >= 0) desired.RemoveAt(keep);
            else _db.VndLinks.Remove(link);
        }

        foreach (var (lang, code, targetId) in desired)
        {
            _db.VndLinks.Add(new VndLink
            {
                SourceVndId = redaction.VndId,
                TargetVndId = targetId,
                Kind = VndLinkKind.LegacyText,
                SourceRedactionId = redaction.Id,
                SourceDocumentTarget = lang,
                LegacyCode = code,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);

        var attachmentRefsValue = attachmentRefs.Count > 0 ? string.Join(';', attachmentRefs) : null;
        if (attachmentRefsValue is { Length: > 1000 }) attachmentRefsValue = attachmentRefsValue[..1000];
        await _db.VndRedactions
            .Where(r => r.Id == redaction.Id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.LegacyAttachmentRefs, attachmentRefsValue)
                .SetProperty(r => r.LegacyLinksScanKey, key), ct);
    }
}

/// <summary>Фоновая индексация легаси-гиперссылок во всех редакциях (см. VndLegacyLinkIndexer):
/// первый проход вскоре после старта (заполняет "Ссылающиеся документы" для уже перенесённых из
/// isrib документов), затем - раз в полчаса, подхватывая новые/заменённые файлы редакций.</summary>
public class VndLegacyLinkIndexWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VndLegacyLinkIndexWorker> _logger;

    public VndLegacyLinkIndexWorker(IServiceScopeFactory scopeFactory, ILogger<VndLegacyLinkIndexWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var total = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Новый scope на каждую пачку - чтобы не копить отслеживаемые сущности в
                    // одном DbContext на тысячах редакций.
                    using var scope = _scopeFactory.CreateScope();
                    var indexer = scope.ServiceProvider.GetRequiredService<IVndLegacyLinkIndexer>();
                    var processed = await indexer.IndexPendingAsync(BatchSize, stoppingToken);
                    total += processed;
                    // Меньше пачки - значит, всё остальное уже актуально (или стабильно падает -
                    // тогда не крутимся в цикле, а ждём следующего тика).
                    if (processed < BatchSize) break;
                }

                if (total > 0)
                    _logger.LogInformation("Проиндексированы легаси-ссылки в {Count} редакциях ВНД", total);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка фоновой индексации легаси-ссылок ВНД");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
