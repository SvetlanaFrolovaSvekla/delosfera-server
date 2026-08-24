using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

/// <summary>
/// Раздаёт права на новые разделы существующим ролям.
///
/// Права заводятся вместе с разделом, но ни одной роли не принадлежат — и раздел
/// после выкладки не видит никто, включая администратора. Разбирать это руками
/// по каждой роли долго и ошибочно: забытое право выглядит как сломанный раздел.
///
/// Выдаётся только то, что роль и так делает по работе. Банковская тайна не
/// раздаётся никому: круг допущенных к запросам по счетам определяет банк, а не
/// умолчание в коде.
/// </summary>
public static class RolePermissionDefaults
{
    /// <summary>
    /// Кому что добавить. Ключ — название роли как оно заведено в справочнике;
    /// сопоставление идёт по вхождению, чтобы «Обкатка: Секретарь коллегиальных
    /// органов» получил то же, что и «Секретарь».
    /// </summary>
    private static readonly (string RoleMatch, PermissionCode[] Add)[] Rules =
    [
        // Администратор ведёт всё, кроме банковской тайны.
        ("Администратор", [
            PermissionCode.ViewPowersOfAttorney,
            PermissionCode.ManagePowersOfAttorney,
            PermissionCode.ViewCorrespondence,
            PermissionCode.RegisterCorrespondence,
            PermissionCode.ViewHrOrders,
            PermissionCode.ManageHrOrders,
        ]),

        // Делопроизводство: книга регистрации — их основная работа.
        ("Делопроизвод", [
            PermissionCode.ViewCorrespondence,
            PermissionCode.RegisterCorrespondence,
            PermissionCode.ViewPowersOfAttorney,
        ]),

        // Секретарь органа отбирает вопросы и видит переписку по своим темам.
        ("Секретарь", [
            PermissionCode.ViewCorrespondence,
            PermissionCode.ViewPowersOfAttorney,
        ]),

        // Руководителю нужен ответ на вопрос «вправе ли он это подписать».
        ("Руководител", [
            PermissionCode.ViewPowersOfAttorney,
            PermissionCode.ViewCorrespondence,
        ]),

        // Методология ведёт нормотворчество; переписка нужна для запросов регулятора.
        ("Методолог", [
            PermissionCode.ViewCorrespondence,
            PermissionCode.ViewPowersOfAttorney,
        ]),

        ("Главный редактор ВНД", [
            PermissionCode.ViewCorrespondence,
            PermissionCode.ViewPowersOfAttorney,
        ]),
    ];

    /// <summary>
    /// Добавляет недостающие права. Существующие не трогает и ничего не отбирает:
    /// то, что банк настроил руками, умолчание переопределять не вправе.
    /// </summary>
    public static async Task ApplyAsync(DelosferaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var roles = await db.Roles.ToListAsync(ct);
        var changed = 0;

        foreach (var role in roles)
        {
            var add = Rules
                .Where(rule => role.TitleRu.Contains(rule.RoleMatch, StringComparison.OrdinalIgnoreCase))
                .SelectMany(rule => rule.Add)
                .Select(code => (int)code)
                .Distinct()
                .Where(code => !role.PermissionCodes.Contains(code))
                .ToArray();

            if (add.Length == 0) continue;

            role.PermissionCodes = role.PermissionCodes.Concat(add).ToArray();
            role.UpdatedAt = DateTime.UtcNow;
            changed++;

            logger.LogInformation(
                "Роль «{Role}»: добавлено прав на новые разделы — {Count}",
                role.TitleRu, add.Length);
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
    }
}
