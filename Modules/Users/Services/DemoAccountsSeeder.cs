using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

/// <summary>Учётная запись для обкатки: кто это по работе и что ему можно.</summary>
public record DemoAccount(
    string Email,
    string FullName,
    string RoleTitle,
    string Purpose,
    PermissionCode[] Permissions);

/// <summary>
/// Учётные записи для обкатки бизнес-подразделениями.
///
/// Смысл не в удобстве входа, а в том, что систему нельзя проверить из-под одной
/// роли. Записка, поданная инициатором, встанет на регистрации, если некому
/// регистрировать; заседание не проведёт тот, у кого нет прав секретаря. Тестировщик
/// из подразделения не должен упираться в «доступ запрещён» там, где на самом деле
/// нужен просто другой человек.
///
/// Включается только настройкой <c>Demo:Enabled</c>. Пароль берётся из
/// <c>Demo:Password</c> и в коде не хранится — в этом проекте уже была миграция,
/// обнулявшая пароли сид-аккаунтов, и повторять ту ошибку не будем. Без пароля в
/// настройках учётки не заводятся вовсе.
/// </summary>
public static class DemoAccountsSeeder
{
    /// <summary>
    /// Домен намеренно нерабочий: на такой адрес ничего не уйдёт, и учётку видно
    /// в любом списке пользователей с одного взгляда.
    /// </summary>
    public const string Domain = "@test.local";

    /// <summary>
    /// Шесть, а не пять: без делопроизводителя поток служебной записки обрывается
    /// на регистрации, и проверить его до конца некому.
    /// </summary>
    public static readonly DemoAccount[] Accounts =
    [
        new(
            "initiator" + Domain,
            "Тестовый Инициатор",
            "Сотрудник",
            "Пишет служебные записки, подаёт заявки на закупку, читает ВНД",
            [
                PermissionCode.ViewVnd,
                PermissionCode.ExportVnd,
                PermissionCode.ViewMeetings,
                PermissionCode.ReportMeetingExecution,
            ]),

        new(
            "manager" + Domain,
            "Тестовый Руководитель",
            "Руководитель подразделения",
            "Согласовывает и подписывает, видит черновики подчинённых и статистику подразделения",
            [
                PermissionCode.ViewVnd,
                PermissionCode.ExportVnd,
                PermissionCode.ActAsApprover,
                PermissionCode.ModifyApprovalRoute,
                PermissionCode.ViewOtherUsersDrafts,
                PermissionCode.ViewLimitedStatistics,
                PermissionCode.ViewMeetings,
                PermissionCode.ReportMeetingExecution,
            ]),

        new(
            "clerk" + Domain,
            "Тестовый Делопроизводитель",
            "Сектор делопроизводства",
            "Регистрирует записки, ведёт бумажные оригиналы и справочники записок",
            [
                PermissionCode.ViewVnd,
                PermissionCode.ManageSzDictionaries,
                PermissionCode.ActAsApprover,
                PermissionCode.ViewOtherUsersDrafts,
                PermissionCode.ViewMeetings,
            ]),

        new(
            "secretary" + Domain,
            "Тестовый Секретарь",
            "Секретарь коллегиальных органов",
            "Ведёт заседания Правления, КПА и Кредитного комитета: повестка, решения, поручения",
            [
                PermissionCode.ViewVnd,
                PermissionCode.ViewMeetings,
                PermissionCode.ManageBoardMeetings,
                PermissionCode.ManageKpaMeetings,
                PermissionCode.ManageCreditCommitteeMeetings,
                PermissionCode.ExportMeetingRegistry,
                PermissionCode.MemberOfBoard,
                PermissionCode.ActAsApprover,
            ]),

        new(
            "methodolog" + Domain,
            "Тестовый Методолог",
            "Отдел методологии",
            "Ведёт базу ВНД и план актуализации, справочники нормотворчества",
            [
                PermissionCode.ViewVnd,
                PermissionCode.ExportVnd,
                PermissionCode.CreateVndWithApproval,
                PermissionCode.CreateVndWithoutApproval,
                PermissionCode.EditVndRequisites,
                PermissionCode.EditLastRevisionDirectly,
                PermissionCode.ActualizeAnyVndWithApproval,
                PermissionCode.ViewVndActualizationPage,
                PermissionCode.ManageVndDictionaries,
                PermissionCode.ActAsApprover,
                PermissionCode.ViewFullStatistics,
                PermissionCode.ExportFullStatisticsReport,
                PermissionCode.ViewMeetings,
            ]),

        new(
            "admin" + Domain,
            "Тестовый Администратор",
            "Администратор системы",
            "Все права: пользователи, роли, справочники, настройки, разбор пожеланий",
            Enum.GetValues<PermissionCode>()),
    ];

    /// <summary>
    /// Заводит роли и учётные записи, если их ещё нет. Существующие не трогает,
    /// кроме пароля: его выравнивают под настройку, иначе после смены пароля в
    /// окружении подсказка на странице входа перестанет соответствовать правде.
    /// </summary>
    public static void Seed(
        DelosferaDbContext db,
        IUserPasswordHasher hasher,
        string password,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Demo:Enabled включён, но Demo:Password не задан — тестовые учётки не заведены");
            return;
        }

        var hash = hasher.Hash(password);
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var account in Accounts)
        {
            var roleTitle = "Обкатка: " + account.RoleTitle;

            var role = db.Roles.FirstOrDefault(r => r.TitleRu == roleTitle);
            if (role is null)
            {
                role = new Role
                {
                    TitleRu = roleTitle,
                    PermissionCodes = account.Permissions.Select(p => (int)p).Distinct().ToArray(),
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Roles.Add(role);
                db.SaveChanges();
                changed = true;
            }

            var user = db.Users
                .Include(u => u.Roles)
                .FirstOrDefault(u => u.Email == account.Email);

            if (user is null)
            {
                user = new User
                {
                    FullName = account.FullName,
                    Email = account.Email,
                    PasswordHash = hash,
                    IsActive = true,
                    Source = UserSource.Local,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                user.Roles.Add(role);
                db.Users.Add(user);
                changed = true;
                continue;
            }

            // Учётка уже есть — приводим в рабочее состояние. Пароль и разблокировка:
            // тестировщики исчерпывают попытки входа регулярно, и запертая учётка
            // на второй день обкатки останавливает подразделение.
            user.PasswordHash = hash;
            user.IsActive = true;
            user.BlockedAt = null;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = now;

            if (user.Roles.All(r => r.Id != role.Id))
                user.Roles.Add(role);

            changed = true;
        }

        if (changed)
        {
            db.SaveChanges();
            logger.LogWarning(
                "Заведены тестовые учётные записи для обкатки ({Count} шт., домен {Domain}). " +
                "На продуктивном контуре Demo:Enabled должен быть выключен.",
                Accounts.Length, Domain);
        }
    }
}
