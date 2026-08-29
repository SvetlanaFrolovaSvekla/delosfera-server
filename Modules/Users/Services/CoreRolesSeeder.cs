using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

/// <summary>
/// Заводит роли органов банка, без которых процессы упираются в некому-нажать.
///
/// Право можно завести в коде и закрыть им кнопку, но если роли-держателя нет в
/// справочнике, действие закрыто наглухо: в журнале это выглядит ровно так же,
/// как работающее право. Так и вышло с вынесением записки на коллегиальный орган —
/// право было, роли Правления не было, и вынести вопрос не мог никто, кроме
/// администратора.
///
/// Сидер создаёт только пустую роль. Кого в неё включить — решает банк: должность
/// в справочнике говорит, кто человек, но не говорит, что ему пора выдать доступ.
/// Права роли раздаёт <see cref="RolePermissionDefaults"/> по названию, поэтому
/// здесь их нет: одно место на весь состав прав.
/// </summary>
public static class CoreRolesSeeder
{
    /// <summary>
    /// Роли, которые должны существовать, и зачем они нужны.
    ///
    /// Правление и его председатель разведены намеренно: реестр записок целиком
    /// видит всё Правление, а выносить вопрос на коллегиальный орган вправе только
    /// председатель или исполняющий его обязанности. И.О. получает доступ выдачей
    /// той же роли на период замещения — отдельной сущности для этого нет.
    /// </summary>
    private static readonly (string TitleRu, string TitleEn)[] Roles =
    [
        ("Правление", "Management Board"),
        ("Председатель Правления", "Chairman of the Management Board"),

        // Регистрация записки — единственный вход в книгу регистрации, и до сих пор
        // роль под неё существовала только с приставкой «Обкатка:». Процесс держался
        // на демонстрационной учётной записи.
        ("Сектор делопроизводства", "Records Management Sector"),

        // Закупки: организатор процедуры и секретарь, оформляющий решения комиссии.
        ("Сектор закупок", "Procurement Sector"),
        ("Секретарь комиссии по закупкам", "Procurement Commission Secretary"),

        // Вопрос, вынесенный на коллегиальный орган, дальше ведёт его секретарь.
        ("Секретарь коллегиальных органов", "Collegial Bodies Secretary"),
    ];

    public static async Task ApplyAsync(DelosferaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.Roles.Select(r => r.TitleRu).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var created = 0;

        foreach (var (titleRu, titleEn) in Roles)
        {
            // Сравнение точное: «Обкатка: Правление» — другая роль, и подменять её
            // боевой нельзя. Совпадение по вхождению здесь означало бы, что состав
            // Правления зависит от того, кто как назвал роль для демонстрации.
            if (existing.Any(t => string.Equals(t, titleRu, StringComparison.OrdinalIgnoreCase)))
                continue;

            db.Roles.Add(new Role
            {
                TitleRu = titleRu,
                TitleEn = titleEn,
                PermissionCodes = [],
                CreatedAt = now,
                UpdatedAt = now,
            });

            created++;
            logger.LogInformation("Заведена роль «{Role}» — состав определяет банк", titleRu);
        }

        if (created > 0)
            await db.SaveChangesAsync(ct);
    }
}
