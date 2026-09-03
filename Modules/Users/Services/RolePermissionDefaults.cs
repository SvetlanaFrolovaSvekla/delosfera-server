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

        // Делопроизводство: книга регистрации — их основная работа. Реестр записок
        // целиком нужен им по той же причине: они его и ведут.
        ("Делопроизвод", [
            PermissionCode.ViewCorrespondence,
            PermissionCode.RegisterCorrespondence,
            PermissionCode.ViewPowersOfAttorney,
            PermissionCode.ViewAllSz,
            PermissionCode.RegisterSz,
        ]),

        // Правление видит реестр записок целиком: до них доходит то, что не решилось
        // на уровне подразделений, и знать об этом заранее — часть работы. Выносить
        // вопрос на коллегиальный орган зампред при этом не вправе — только
        // председатель, поэтому здесь одно право, а не два.
        ("Правлени", [
            PermissionCode.ViewAllSz,

            // Признак состава органа: по нему собирается поле «Кому» у записки на
            // Правление и полная повестка заседания. Раньше он попадал в «все
            // права» и доставался администраторам, а членам Правления — нет.
            PermissionCode.MemberOfBoard,
        ]),

        // Председатель и исполняющий его обязанности: вынести подписанную записку
        // на коллегиальный орган может только он. Правило срабатывает и на роли
        // «Председатель Правления», которая по названию попадает и в правило выше.
        ("Председател", [
            PermissionCode.ViewAllSz,
            PermissionCode.SubmitSzToBody,
            PermissionCode.MemberOfBoard,
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

        // ── Закупки ──────────────────────────────────────────────────────────
        //
        // Заявку заводит любой сотрудник, поэтому права на это нет. Дальше
        // процесс расходится по ролям: Сектор закупок ведёт процедуру,
        // секретарь комиссии оформляет её решения, УПиА смотрит бюджет.

        ("Администратор", [
            PermissionCode.ViewAllSz,
            PermissionCode.RegisterSz,
            PermissionCode.SubmitSzToBody,
            PermissionCode.ViewAllProcurements,
            PermissionCode.ConductProcurement,
            PermissionCode.RecordCommissionDecisions,
            PermissionCode.ManageProcurementProtocol,
            PermissionCode.ManageProcurementContracts,
            PermissionCode.ManageProcurementPlan,
            PermissionCode.ManageSuppliers,
        ]),

        // Сектор закупок — организатор: конкурс, комиссия, публикация, договоры,
        // поставщики. Голоса за комиссию он не вносит: это дело её секретаря.
        ("Сектор закупок", [
            PermissionCode.ViewAllProcurements,
            PermissionCode.ConductProcurement,
            PermissionCode.ManageProcurementProtocol,
            PermissionCode.ManageProcurementContracts,
            PermissionCode.ManageProcurementPlan,
            PermissionCode.ManageSuppliers,
        ]),

        // Секретарь комиссии ведёт протокол заседания: явка, голоса, заключения.
        ("Секретарь комиссии", [
            PermissionCode.ViewAllProcurements,
            PermissionCode.RecordCommissionDecisions,
            PermissionCode.ManageProcurementProtocol,
        ]),

        // УПиА визирует бюджет по маршруту, отдельного права на это не нужно —
        // но чтобы визировать, надо видеть чужие заявки, а не только свои.
        ("УПиА", [
            PermissionCode.ViewAllProcurements,
        ]),

        ("бюджетир", [
            PermissionCode.ViewAllProcurements,
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
