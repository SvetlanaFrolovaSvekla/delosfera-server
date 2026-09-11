using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Security;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using User = delosfera_server.Modules.Users.Models.User;

namespace delosfera_server.Modules.Integrations.OrgStructure;

public interface IOrgSyncService
{
    Task<OrgSyncRun> RunAsync(int? startedByUserId, CancellationToken ct = default);
    Task<int> CheckConnectionAsync(string portalUrl, string? token, CancellationToken ct = default);
}

/// <summary>
/// Переносит оргструктуру из портала в справочник подразделений и расставляет
/// сотрудников по местам.
///
/// Что делает и чего не делает:
///
/// — подразделения заводит и обновляет, но <b>не удаляет</b>. Исчезло из портала —
///   значит расформировано, а к нему привязаны документы, маршруты и записки.
///   Их нельзя оставить без подразделения, и решать судьбу такого узла должен
///   человек, а не ночной проход;
///
/// — сотрудников <b>не заводит</b>. В портале весь банк, а в системе — те, кому
///   выдали доступ. Заводить всех означало бы четыреста учётных записей, которыми
///   никто не пользуется, и в отчёте о посещаемости — четыреста «ни разу не заходил»;
///
/// — должности заводит: без них у половины людей должность оказалась бы пустой,
///   а справочник должностей ведут не так строго, как подразделения.
/// </summary>
public class OrgSyncService(
    DelosferaDbContext db,
    PortalOrgClient portal,
    ISecretProtector protector,
    ILogger<OrgSyncService> log) : IOrgSyncService
{
    public async Task<int> CheckConnectionAsync(
        string portalUrl, string? token, CancellationToken ct = default)
    {
        // Пустой токен в проверке означает «взять сохранённый»: администратор
        // проверяет связь, не вводя токен заново, — он и не может, токен
        // показывают один раз при выдаче.
        if (string.IsNullOrWhiteSpace(token))
        {
            var saved = await db.OrgStructureSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (saved is null || string.IsNullOrEmpty(saved.TokenEncrypted))
                throw new PortalException("Токен не задан.");

            token = protector.Unprotect(saved.TokenEncrypted);
        }

        return await portal.CheckAsync(portalUrl, token!, ct);
    }

    public async Task<OrgSyncRun> RunAsync(int? startedByUserId, CancellationToken ct = default)
    {
        var settings = await db.OrgStructureSettings.FirstOrDefaultAsync(ct)
            ?? throw new PortalException("Связь с порталом не настроена.");

        var run = new OrgSyncRun
        {
            StartedAt = DateTime.UtcNow,
            StartedByUserId = startedByUserId,
            Outcome = OrgSyncOutcome.Failed,
        };
        db.OrgSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var notes = new List<string>();

        try
        {
            if (!settings.Enabled)
                throw new PortalException("Синхронизация выключена в настройках.");
            if (string.IsNullOrWhiteSpace(settings.PortalUrl))
                throw new PortalException("Адрес портала не задан.");
            if (string.IsNullOrEmpty(settings.TokenEncrypted))
                throw new PortalException("Токен не задан.");

            var token = protector.Unprotect(settings.TokenEncrypted);

            var units = await portal.GetUnitsAsync(settings.PortalUrl, token, ct);
            var employees = await portal.GetEmployeesAsync(settings.PortalUrl, token, ct);

            run.UnitsReceived = units.Count;
            run.EmployeesReceived = employees.Count;

            var byExternalId = await SyncUnitsAsync(units, settings, run, notes, ct);
            await SyncEmployeesAsync(employees, byExternalId, settings, run, notes, ct);
            await LinkUnitHeadsAsync(units, byExternalId, notes, ct);
            await LinkManagersAsync(employees, settings, run, notes, ct);

            await db.SaveChangesAsync(ct);

            run.Outcome = run.EmployeesUnmatched > 0 || run.UnitsSkipped > 0
                ? OrgSyncOutcome.Partial
                : OrgSyncOutcome.Success;
        }
        catch (PortalException e)
        {
            run.Outcome = OrgSyncOutcome.Failed;
            run.Error = e.Message;
            log.LogWarning("Синхронизация оргструктуры не прошла: {Error}", e.Message);
        }
        catch (OperationCanceledException)
        {
            // Запрос оборвали — обычно это истёкшее ожидание у обратного прокси.
            // Проход при этом мог сделать половину работы, и запись о нём должна
            // сказать об этом прямо: иначе в истории висит «не прошла» без
            // времени окончания и без причины, и понять, что случилось, нельзя.
            run.Outcome = OrgSyncOutcome.Failed;
            run.Error = "Проход прерван: обращение к порталу не уложилось в отведённое время. "
                      + "Часть данных могла обновиться. Запустите синхронизацию заново.";
            log.LogWarning("Синхронизация оргструктуры прервана по времени");
        }
        catch (Exception e)
        {
            run.Outcome = OrgSyncOutcome.Failed;
            run.Error = "Непредвиденная ошибка: " + e.Message;
            log.LogError(e, "Синхронизация оргструктуры прервалась");
        }

        run.FinishedAt = DateTime.UtcNow;
        if (notes.Count > 0)
            run.NotesJson = JsonSerializer.Serialize(notes.Take(200), JsonOptions);

        // Запись о проходе дописываем признаком, который не отменяют. Прежде
        // здесь стоял тот же признак, что и у запроса: прокси обрывал ожидание,
        // отмена доходила и сюда, сохранение падало — и в истории оставалась
        // строка без времени окончания и без объяснения.
        await db.SaveChangesAsync(CancellationToken.None);
        return run;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Вид узла по названию из портала. Неизвестное значение не роняет проход
    /// и не подменяется догадкой: портал может завести новый вид, и лучше
    /// оставить «не указан», чем записать неверный.
    /// </summary>
    private static OrgUnitKind ВидПоПорталу(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "board" => OrgUnitKind.Board,
        "division" => OrgUnitKind.Division,
        "department" => OrgUnitKind.Department,
        _ => OrgUnitKind.Unknown,
    };

    /// <summary>
    /// Заводит и обновляет подразделения. Возвращает соответствие
    /// «идентификатор в портале → подразделение у нас».
    /// </summary>
    private async Task<Dictionary<int, OrganizationUnit>> SyncUnitsAsync(
        List<PortalUnit> units, OrgStructureSettings settings,
        OrgSyncRun run, List<string> notes, CancellationToken ct)
    {
        var existing = await db.OrganizationUnits.ToListAsync(ct);
        var byExternal = existing
            .Where(u => u.ExternalId is not null)
            .ToDictionary(u => u.ExternalId!.Value);

        // Первый проход: подразделения без связи с порталом сопоставляем по
        // названию — иначе при первом включении вся структура задвоится.
        // Дальше сопоставление идёт только по идентификатору.
        var byTitle = existing
            .Where(u => u.ExternalId is null)
            .GroupBy(u => u.TitleRu.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        foreach (var portalUnit in units)
        {
            var вид = ВидПоПорталу(portalUnit.Kind);

            if (byExternal.TryGetValue(portalUnit.Id, out var unit))
            {
                if (unit.TitleRu != portalUnit.Name || unit.Kind != вид)
                {
                    unit.TitleRu = portalUnit.Name;
                    unit.Kind = вид;
                    unit.UpdatedAt = now;
                    run.UnitsUpdated++;
                }
                continue;
            }

            if (byTitle.TryGetValue(portalUnit.Name.Trim(), out var matched))
            {
                matched.ExternalId = portalUnit.Id;
                matched.Kind = вид;
                matched.UpdatedAt = now;
                byExternal[portalUnit.Id] = matched;
                run.UnitsUpdated++;
                continue;
            }

            if (!settings.CreateMissingUnits)
            {
                run.UnitsSkipped++;
                notes.Add($"Подразделение «{portalUnit.Name}» есть в портале, но не заведено здесь.");
                continue;
            }

            var created = new OrganizationUnit
            {
                TitleRu = portalUnit.Name,
                Kind = вид,
                ExternalId = portalUnit.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.OrganizationUnits.Add(created);
            byExternal[portalUnit.Id] = created;
            run.UnitsCreated++;
        }

        // Идентификаторы новым записям присваивает база — до сохранения связать
        // родителя с ребёнком нельзя.
        await db.SaveChangesAsync(ct);

        foreach (var portalUnit in units)
        {
            if (!byExternal.TryGetValue(portalUnit.Id, out var unit)) continue;

            var parentId = portalUnit.ParentUnitId is int pid && byExternal.TryGetValue(pid, out var parent)
                ? parent.Id
                : (int?)null;

            if (portalUnit.ParentUnitId is not null && parentId is null)
                notes.Add($"У подразделения «{portalUnit.Name}» вышестоящее не нашлось — оставлено без места в структуре.");

            // Само себе родителем подразделение стать не может: одна такая связь
            // делает дерево бесконечным при обходе.
            if (parentId == unit.Id)
            {
                notes.Add($"Подразделение «{portalUnit.Name}» подчинено само себе — связь не проставлена.");
                parentId = null;
            }

            if (unit.ParentId != parentId)
            {
                unit.ParentId = parentId;
                unit.UpdatedAt = now;
            }
        }

        return byExternal;
    }

    /// <summary>
    /// Расставляет сотрудников: подразделение, должность, признак работы.
    /// Новых учётных записей не заводит.
    /// </summary>
    private async Task SyncEmployeesAsync(
        List<PortalEmployee> employees, Dictionary<int, OrganizationUnit> unitsByExternal,
        OrgStructureSettings settings, OrgSyncRun run, List<string> notes, CancellationToken ct)
    {
        var users = await db.Users.ToListAsync(ct);

        var byEmail = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var byLogin = users
            .Where(u => !string.IsNullOrWhiteSpace(u.LdapLogin))
            .GroupBy(u => u.LdapLogin!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var positions = await db.Positions.ToListAsync(ct);
        var positionsByTitle = positions
            .GroupBy(p => p.TitleRu.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        // Кого предстоит отключить. Собираем списком, а не гасим на месте:
        // решение зависит от их числа, а оно известно только в конце обхода.
        var toDeactivate = new List<User>();

        foreach (var employee in employees)
        {
            var user = Match(employee);
            if (user is null)
            {
                run.EmployeesUnmatched++;
                continue;
            }

            run.EmployeesMatched++;
            var changed = false;

            if (employee.Unit is { } unitRef
                && unitsByExternal.TryGetValue(unitRef.Id, out var unit)
                && user.OrgUnitId != unit.Id)
            {
                user.OrgUnitId = unit.Id;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(employee.Title))
            {
                var title = employee.Title.Trim();
                if (!positionsByTitle.TryGetValue(title, out var position))
                {
                    position = new Position { TitleRu = title, CreatedAt = now, UpdatedAt = now };
                    db.Positions.Add(position);
                    positionsByTitle[title] = position;
                    notes.Add($"Заведена должность «{title}».");
                }

                // У только что заведённой должности идентификатора ещё нет —
                // связь проставим после сохранения, по ссылке на объект.
                if (position.Id != 0 && user.PositionId != position.Id)
                {
                    user.PositionId = position.Id;
                    changed = true;
                }
                else if (position.Id == 0)
                {
                    user.Position = position;
                    changed = true;
                }
            }

            // Уволенных в портале гасим и здесь: иначе они остаются в списках
            // выбора согласующих, и записка уходит человеку, которого нет.
            //
            // Само отключение делаем ниже, разом: сначала нужно знать, скольких
            // оно затронет. Один сбойный ответ портала не должен закрыть доступ
            // всему банку.
            if (user.IsActive && !employee.Active)
            {
                toDeactivate.Add(user);
            }
            else if (!user.IsActive && employee.Active)
            {
                // Вернулся на работу — доступ возвращаем сразу, без оглядки:
                // ошибка здесь никого не запирает.
                user.IsActive = true;
                changed = true;
            }

            if (changed)
            {
                user.UpdatedAt = now;
                run.EmployeesUpdated++;
            }
        }

        // Предохранитель. Если портал по какой-то причине пометил уволенными
        // добрую половину сопоставленных, это скорее его сбой, чем реорганизация:
        // в банке столько разом не увольняют. Такое отключение не выполняем —
        // записываем замечание, и пусть решает человек. Обратная ошибка дороже:
        // вернуть доступ пятистам сотрудникам вручную — это рабочий день.
        var предел = Math.Max(5, run.EmployeesMatched / 2);

        if (toDeactivate.Count > предел)
        {
            notes.Add(
                $"Портал пометил уволенными {toDeactivate.Count} из {run.EmployeesMatched} сопоставленных — " +
                "это больше половины, и на реорганизацию не похоже. Доступ никому не закрыт. " +
                "Проверьте портал и запустите синхронизацию заново.");
        }
        else
        {
            foreach (var user in toDeactivate)
            {
                user.IsActive = false;
                user.UpdatedAt = now;
                run.EmployeesDeactivated++;
            }
        }

        User? Match(PortalEmployee employee)
        {
            // Почта устойчивее логина: при смене фамилии логин в домене иногда
            // меняют, а почта остаётся той же и в домене, и в портале.
            if (settings.MatchByEmail
                && !string.IsNullOrWhiteSpace(employee.Email)
                && byEmail.TryGetValue(employee.Email.Trim(), out var byMail))
                return byMail;

            if (!string.IsNullOrWhiteSpace(employee.Login)
                && byLogin.TryGetValue(employee.Login.Trim(), out var byDomainLogin))
                return byDomainLogin;

            return null;
        }
    }

    /// <summary>
    /// Проставляет личных руководителей — тех, что указаны в карточке портала.
    ///
    /// Отдельным проходом и последним: руководитель — тоже сотрудник, и найти
    /// его можно лишь после того, как расставлены все.
    ///
    /// Портал считает подчинённость по трём правилам. Второе и третье выводятся
    /// из структуры — у нас их даёт начальник подразделения и куратор. Здесь
    /// переносится только первое: руководитель, назначенный человеку лично.
    /// Без него согласование уходило бы начальнику подразделения и там, где банк
    /// назначил другого.
    /// </summary>
    private async Task LinkManagersAsync(
        List<PortalEmployee> employees, OrgStructureSettings settings,
        OrgSyncRun run, List<string> notes, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);

        var users = await db.Users.ToListAsync(ct);

        var byEmail = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var byLogin = users
            .Where(u => !string.IsNullOrWhiteSpace(u.LdapLogin))
            .GroupBy(u => u.LdapLogin!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var ненайденные = new HashSet<string>();

        foreach (var employee in employees)
        {
            var user = Найти(employee.Email, employee.Login);
            if (user is null) continue;

            // Пустой руководитель — не ошибка портала, а состояние структуры:
            // у первого лица его нет, а новому сотруднику могли не назначить.
            // Значит и здесь не затираем: было бы обидно потерять проставленное
            // руками из-за того, что портал промолчал.
            if (employee.Manager is null) continue;

            var manager = Найти(null, employee.Manager.Login);
            if (manager is null)
            {
                // Руководитель есть в портале, но доступа в систему у него нет —
                // обычное дело, в портале весь банк. Отмечаем и идём дальше.
                ненайденные.Add(employee.Manager.Name);
                continue;
            }

            // Сам себе руководителем человек стать не может: обход цепочки
            // согласования по такой связи не кончится никогда.
            if (manager.Id == user.Id)
            {
                notes.Add($"У «{employee.Name}» руководителем указан он сам — связь не проставлена.");
                continue;
            }

            if (user.ManagerId != manager.Id)
            {
                user.ManagerId = manager.Id;
                user.UpdatedAt = now;
                run.EmployeesUpdated++;
            }
        }

        if (ненайденные.Count > 0)
        {
            notes.Add(
                $"Руководителей из портала не нашлось среди пользователей: {ненайденные.Count}. " +
                "Это не ошибка: в портале весь банк, а доступ выдан не всем. " +
                $"Например: {string.Join(", ", ненайденные.Take(3))}.");
        }

        User? Найти(string? email, string? login)
        {
            if (settings.MatchByEmail
                && !string.IsNullOrWhiteSpace(email)
                && byEmail.TryGetValue(email.Trim(), out var byMail))
                return byMail;

            if (!string.IsNullOrWhiteSpace(login)
                && byLogin.TryGetValue(login.Trim(), out var byDomainLogin))
                return byDomainLogin;

            return null;
        }
    }

    /// <summary>
    /// Проставляет начальников подразделений и кураторов. Отдельным проходом:
    /// начальник — сотрудник, а сотрудников расставили только что.
    /// </summary>
    private async Task LinkUnitHeadsAsync(
        List<PortalUnit> units, Dictionary<int, OrganizationUnit> unitsByExternal,
        List<string> notes, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);

        var users = await db.Users.ToListAsync(ct);
        var byLogin = users
            .Where(u => !string.IsNullOrWhiteSpace(u.LdapLogin))
            .GroupBy(u => u.LdapLogin!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        foreach (var portalUnit in units)
        {
            if (!unitsByExternal.TryGetValue(portalUnit.Id, out var unit)) continue;

            // Пустой руководитель — не ошибка портала, а состояние структуры:
            // у первого лица его нет, а новому подразделению могли не назначить.
            // Поэтому пустое значение не затирает уже проставленного здесь.
            if (portalUnit.Head is { } head
                && byLogin.TryGetValue(head.Login.Trim(), out var headUser)
                && unit.HeadUserId != headUser.Id)
            {
                unit.HeadUserId = headUser.Id;
                unit.UpdatedAt = now;
            }

            if (portalUnit.ParentHead is { } curator
                && byLogin.TryGetValue(curator.Login.Trim(), out var curatorUser)
                && unit.CuratorUserId != curatorUser.Id)
            {
                unit.CuratorUserId = curatorUser.Id;
                unit.UpdatedAt = now;
            }
            else if (portalUnit.ParentHead is { } missing
                     && !byLogin.ContainsKey(missing.Login.Trim()))
            {
                notes.Add($"Куратор «{missing.Name}» подразделения «{portalUnit.Name}» не найден среди пользователей.");
            }
        }
    }
}
