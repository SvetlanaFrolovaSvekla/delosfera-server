using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Options;
using Microsoft.Extensions.Options;

namespace delosfera_server.Common.Services.Authorization.Ldap;

public record LdapSyncResult(int Created, int Updated, int Deactivated);

public class LdapUserSyncService
{
    private readonly DelosferaDbContext _db;
    private readonly ILdapDirectoryService _directory;
    private readonly LdapOptions _options;
    private readonly ILogger<LdapUserSyncService> _logger;

    public LdapUserSyncService(
        DelosferaDbContext db, ILdapDirectoryService directory,
        IOptions<LdapOptions> options, ILogger<LdapUserSyncService> logger)
    {
        _db = db;
        _directory = directory;
        _options = options.Value;
        _logger = logger;
    }

    /*Сколько юзеров создано, сколько обновлено, сколько деактивировано за этот 
     прогон синка. Возвращается как JSON админу, который нажал кнопку "синхронизировать".*/
    public async Task<LdapSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var ldapUsers = await _directory.GetAllUsersAsync(ct);
        // превращает список в словарь, чтоб можно было быстро проверить есть ли этот GUID среди свежих данных из AD
        var ldapById = ldapUsers.ToDictionary(x => x.ObjectId);
        
        /* existingLdapUsers - то, что уже лежит в БД, причём только те 
        записи, у которых Source == UserSource.Ldap - то есть только те юзеры, 
        что раньше уже были заведены через LDAP-синк, а не локальные учётки*/
        var existingLdapUsers = await _db.Users 
            .Where(x => x.Source == UserSource.Ldap)
            .ToListAsync(ct);

        // Должность и подразделение приходят из домена названиями, а в системе это
        // записи справочников. Сопоставляем по наименованию: заводить их заново на
        // каждую синхронизацию нельзя — справочники ведёт делопроизводство.
        var positions = await _db.Positions.ToListAsync(ct);
        var units = await _db.OrganizationUnits.ToListAsync(ct);

        var несопоставленныеДолжности = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var несопоставленныеПодразделения = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int? НайтиДолжность(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var найдено = positions.FirstOrDefault(p => p.TitleRu.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (найдено is null) несопоставленныеДолжности.Add(title);
            return найдено?.Id;
        }

        int? НайтиПодразделение(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var найдено = units.FirstOrDefault(u => u.TitleRu.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (найдено is null) несопоставленныеПодразделения.Add(title);
            return найдено?.Id;
        }

        int created = 0, updated = 0, deactivated = 0;

        // Создать / обновить
        /*Для каждого юзера, пришедшего из AD, ищем: есть ли у нас уже локальная
        запись с таким же LdapObjectId (неизменный objectGUID)*/
        foreach (var ldapUser in ldapUsers)
        {
            var local = existingLdapUsers.FirstOrDefault(x => x.LdapObjectId == ldapUser.ObjectId);

            // Если такой пользователь не найден - создается новый
            if (local is null)
            {
                // защита от дублей, если email уже занят локальной учёткой
                // (в User модели уже есть уникальный индекс на email)
                
                
                /* Если этот юзер, например, раньше был заведён вручную как локальная учётка 
                (Source = Local) с тем же email, а потом того же человека добавили в AD
                    - при попытке создать нового юзера с тем же email БД выкинет ошибку
                нарушения уникальности. Здесь это отловлено заранее, и вместо падения синка
                    - просто warning в лог, а этот конкретный юзер из AD пропускается
                (continue — переходим к следующей итерации цикла, минуя код ниже). */
                var emailTaken = await _db.Users.AnyAsync(x => x.Email == ldapUser.Email, ct);
                if (emailTaken)
                {
                    _logger.LogWarning("LDAP sync: email {Email} уже занят другой учёткой, пропуск", ldapUser.Email);
                    continue;
                }

                // Если email свободен, то создается новая запись
                _db.Users.Add(new User
                {
                    FullName = ldapUser.FullName,
                    Email = ldapUser.Email,
                    LdapLogin = ldapUser.Login,
                    PasswordHash = string.Empty, // не используется для Source=Ldap
                    Source = UserSource.Ldap,
                    LdapObjectId = ldapUser.ObjectId,
                    IsActive = ldapUser.IsActive,
                    PositionId = НайтиДолжность(ldapUser.Position),
                    OrgUnitId = НайтиПодразделение(ldapUser.Department),
                    Roles = _options.DefaultRoleId.HasValue
                        ? await _db.Roles.Where(r => r.Id == _options.DefaultRoleId.Value).ToListAsync(ct)
                        : []
                });
                created++;
            }
            // Если такой пользователь найден - его данные обновляются
            else
            {
                var positionId = НайтиДолжность(ldapUser.Position);
                var unitId = НайтиПодразделение(ldapUser.Department);

                var changed = local.FullName != ldapUser.FullName
                    || local.Email != ldapUser.Email
                    || local.LdapLogin != ldapUser.Login
                    || local.IsActive != ldapUser.IsActive
                    || (positionId is not null && local.PositionId != positionId)
                    || (unitId is not null && local.OrgUnitId != unitId);

                local.FullName = ldapUser.FullName;
                local.Email = ldapUser.Email;
                local.LdapLogin = ldapUser.Login;
                local.IsActive = ldapUser.IsActive;

                // Пустое значение в домене не стирает того, что уже проставлено
                // руками: каталог дополняет карточку, а не обнуляет её.
                if (positionId is not null) local.PositionId = positionId;
                if (unitId is not null) local.OrgUnitId = unitId;

                if (changed) updated++;
            }
        }

        // Деактивировать тех, кто пропал из AD (уволены/удалены)
        // кто пропал из AD, хотя раньше у нас числился как LDAP-юзер
        foreach (var local in existingLdapUsers)
        {
            if (local.LdapObjectId.HasValue && !ldapById.ContainsKey(local.LdapObjectId.Value) && local.IsActive)
            {
                local.IsActive = false;
                deactivated++;
                _logger.LogInformation("LDAP sync: {Email} деактивирован — не найден в директории", local.Email);
            }
        }
        
        /* Реальный поход в БД — единой транзакцией отправляются все накопленные изменения: 
        и INSERT-ы новых юзеров, и UPDATE-ы изменившихся, и UPDATE-ы деактивированных. */
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("LDAP sync завершён: создано {Created}, обновлено {Updated}, деактивировано {Deactivated}",
            created, updated, deactivated);

        // Названия, которых нет в справочниках, называем поимённо: иначе сотрудники
        // молча останутся без должности, и никто не поймёт почему.
        if (несопоставленныеДолжности.Count > 0)
            _logger.LogWarning("LDAP sync: должности не найдены в справочнике ({Count}): {Titles}",
                несопоставленныеДолжности.Count, string.Join("; ", несопоставленныеДолжности.Take(20)));

        if (несопоставленныеПодразделения.Count > 0)
            _logger.LogWarning("LDAP sync: подразделения не найдены в справочнике ({Count}): {Titles}",
                несопоставленныеПодразделения.Count, string.Join("; ", несопоставленныеПодразделения.Take(20)));

        return new LdapSyncResult(created, updated, deactivated);
    }
}