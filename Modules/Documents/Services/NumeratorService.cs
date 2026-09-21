using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

public class NumeratorService : INumeratorService
{
    private static readonly Regex Placeholder = new(@"\{(\w+)(?::([^}]+))?\}", RegexOptions.Compiled);

    private readonly DelosferaDbContext _db;

    public NumeratorService(DelosferaDbContext db) => _db = db;

    public async Task<string> NextAsync(
        DocumentType type, string scope, string scopeKey, string pattern,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        // Атомарная выдача номера: блокировка строки нумератора (FOR UPDATE),
        // чтобы параллельные регистрации не выдали одинаковый seq.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var typeName = type.ToString();
            var num = await _db.Numerators
                .FromSqlInterpolated(
                    $@"SELECT * FROM numerator
                       WHERE document_type = {typeName} AND scope = {scope} AND scope_key = {scopeKey}
                       FOR UPDATE")
                .FirstOrDefaultAsync();

            if (num is null)
            {
                num = new Numerator
                {
                    DocumentType = type, Scope = scope, ScopeKey = scopeKey, Pattern = pattern, NextSeq = 1
                };
                _db.Numerators.Add(num);
                await _db.SaveChangesAsync();
            }

            var seq = num.NextSeq;
            num.NextSeq = seq + 1;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Format(num.Pattern, seq, scopeKey, tokens);
        });
    }

    // Год в номере берём из года документа (явный токен "year"), затем из год-скоупа
    // нумератора (scope_key вида "2026"), и только в крайнем случае — текущий год.
    // Иначе документ, зарегистрированный задним числом или в декабре под следующий год,
    // получил бы в номере текущий год, разойдясь со своим счётчиком и датой регистрации.
    private static string Format(
        string pattern, int seq, string scopeKey, IReadOnlyDictionary<string, string>? tokens) =>
        Placeholder.Replace(pattern, m =>
        {
            var name = m.Groups[1].Value;
            var fmt = m.Groups[2].Success ? m.Groups[2].Value : null;
            return name switch
            {
                "seq" => fmt is null ? seq.ToString() : seq.ToString(fmt),
                "year" => ResolveYear(scopeKey, tokens),
                _ => tokens is not null && tokens.TryGetValue(name, out var v) ? v : m.Value
            };
        });

    private static string ResolveYear(string scopeKey, IReadOnlyDictionary<string, string>? tokens)
    {
        if (tokens is not null && tokens.TryGetValue("year", out var explicitYear)
            && !string.IsNullOrWhiteSpace(explicitYear))
            return explicitYear;
        if (scopeKey.Length == 4 && int.TryParse(scopeKey, out _))
            return scopeKey;
        return DateTime.UtcNow.Year.ToString();
    }
}
