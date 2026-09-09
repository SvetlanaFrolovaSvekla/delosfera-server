using delosfera_server.Data;
using delosfera_server.Extensions;
using delosfera_server.Common.Middleware;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text;
using delosfera_server.Common.Options;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Common.Services.Authorization.Ldap;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Minio.DataModel.Args;

/*Создается построитель приложения, собирает настройки, переменные окружения*/
var builder = WebApplication.CreateBuilder(args);

// Fail-fast: критичные секреты должны быть заданы (env / user-secrets), а не захардкожены.
// JWT-ключ подписывает все токены — слабый или пустой ключ = возможность подделать любой токен.
static string RequireSecret(IConfiguration cfg, string key, int minLength = 1)
{
    var value = cfg[key];
    if (string.IsNullOrWhiteSpace(value) || value.Length < minLength)
        throw new InvalidOperationException(
            $"Конфигурация '{key}' не задана или короче {minLength} символов. " +
            "Задайте её через переменные окружения или dotnet user-secrets (см. appsettings.example.json).");
    return value;
}

var jwtKey = RequireSecret(builder.Configuration, "Jwt:Key", minLength: 32);
RequireSecret(builder.Configuration, "ConnectionStrings:DefaultConnection");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Delosfera API";
        document.Info.Description = "СЭД банка";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

builder.Services.AddScoped<ILanguageResolver, LanguageResolver>();
builder.Services.AddHttpContextAccessor();

/*Берем Ldap из конфига и распаковываем его в объект LdapOptions -
по совпадению имён свойств. "Server" в JSON в Server в классе, "Port" в Port,
и так далее. Дальше любой сервис, которому нужны эти настройки, просто просит их через DI*/
builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection("Ldap"));


builder.Services.AddScoped<ILdapDirectoryService, LdapDirectoryService>();
builder.Services.AddScoped<LdapUserSyncService>();


// Чтобы не засорять логи и не гонять фоновые попытки конекта с LDAP
if (builder.Configuration.GetValue<bool>("Ldap:Enabled"))
{
    builder.Services.AddHostedService<LdapSyncBackgroundService>();
}


builder.Services.AddScoped<ILdapAuthenticator, LdapAuthenticator>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Календарные даты (сроки, периоды замещения, даты документов) считаются по времени
// банка, а не по UTC: иначе «сегодня» наступает с шестичасовым сдвигом.
builder.Services.AddSingleton<IBankClock, BankClock>();

// Парольная политика и блокировка после неудачных попыток (NFR-03)
builder.Services.Configure<delosfera_server.Common.Security.PasswordPolicyOptions>(
    builder.Configuration.GetSection(delosfera_server.Common.Security.PasswordPolicyOptions.Section));
builder.Services.AddSingleton<delosfera_server.Common.Security.IPasswordPolicy,
    delosfera_server.Common.Security.PasswordPolicy>();
builder.Services.AddScoped<IFileStorageService, MinioFileStorageService>();
builder.Services.AddScoped<IFixedApprovalUnitResolver, FixedApprovalUnitResolver>();
builder.Services.AddScoped<IVndApprovalService, VndApprovalService>();
builder.Services.AddSingleton<IApprovalSheetGenerator, ApprovalSheetGenerator>();
builder.Services.AddHostedService<VndApprovalTimeoutBackgroundService>();

// Отзыв сертификата происходит между подписаниями: узнать о нём система должна
// раньше, чем человек снова придёт подписывать (Б-18).
builder.Services.AddHostedService<delosfera_server.Modules.Signing.Services.CertificateRevocationWorker>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITasksService, TasksService>();
builder.Services.AddScoped<ICoordinationDefaultApproverService, CoordinationDefaultApproverService>();
builder.Services.AddScoped<IVndActualizationService, VndActualizationService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// История документа поверх технического аудита: журнал действий по всем
// контурам, а не только по ВНД.
builder.Services.AddScoped<
    delosfera_server.Modules.ActivityLog.Services.IDocumentHistoryService,
    delosfera_server.Modules.ActivityLog.Services.DocumentHistoryService>();

builder.AddDatabase();
builder.AddDictionaryServices();
builder.AddVndServices();
builder.AddAnalyticsServices();

// Контур СЗ: фундамент документов → движок согласования → ЭП → служебные записки
builder.AddDocumentServices();
builder.AddWorkflowServices();
builder.AddSigningServices();
builder.AddSzServices();
builder.AddProcurementServices();
builder.AddMeetingServices();
builder.AddIntegrationServices();
builder.AddSearchServices();

// Разовую работу при первом обращении к данным делаем заранее и вхолостую:
// иначе она достаётся тому, кто первым открыл раздел после выкладки.
builder.Services.AddHostedService<delosfera_server.Common.Services.WarmupWorker>();

// Обкатка подразделениями: пожелания с экранов и учёт посещаемости. Журнал заходов
// растёт быстрее всех таблиц, поэтому вместе со сбором сразу заводим и чистку.
builder.Services.AddHostedService<delosfera_server.Modules.Feedback.Services.PageVisitCleanupWorker>();

// Доверенности. Состояние хранится, а не считается по датам, — значит кто-то должен
// закрывать истёкшие, иначе реестр отвечает неправдой на вопрос «вправе ли он».
builder.Services.AddScoped<
    delosfera_server.Modules.PowerOfAttorney.Services.IPoaService,
    delosfera_server.Modules.PowerOfAttorney.Services.PoaService>();
builder.Services.AddHostedService<delosfera_server.Modules.PowerOfAttorney.Services.PoaExpiryWorker>();

// Регулярные обязательства: календарь заседаний комитетов, отчётов и пересмотра
// политик. Периоды заводятся вперёд, заседания закрывают их сами.
builder.Services.AddScoped<
    delosfera_server.Modules.Obligations.Services.IObligationService,
    delosfera_server.Modules.Obligations.Services.ObligationService>();
builder.Services.AddHostedService<delosfera_server.Modules.Obligations.Services.ObligationWorker>();

// Канцелярия: книга регистрации входящих и исходящих. Запросы регулятора и
// обращения клиентов — категории писем, а не отдельные реестры.
builder.Services.AddScoped<
    delosfera_server.Modules.Correspondence.Services.ILetterService,
    delosfera_server.Modules.Correspondence.Services.LetterService>();


// Адреса фронтенда задаются конфигурацией: на стенде это localhost, в банке —
// адрес развёрнутого клиента. Захардкоженный localhost означал бы, что на любом
// другом сервере вход не работает, а причина видна только в консоли браузера.
//
// Когда клиент и API стоят за одним reverse-proxy (один origin), CORS не участвует
// вовсе — список нужен лишь для раздельных адресов.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // нужно для httpOnly refresh-cookie (origin'ы заданы явно, не *)
    });
});

builder.Services.AddSingleton<IMinioClient>(_ =>
    new MinioClient()
        .WithEndpoint(builder.Configuration["Minio:Endpoint"])
        .WithCredentials(builder.Configuration["Minio:AccessKey"], builder.Configuration["Minio:SecretKey"])
        .WithSSL(builder.Configuration.GetValue<bool>("Minio:UseSSL"))
        .Build());

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; 
});

builder.AddUserServices();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Health-check для мониторинга/оркестратора (liveness).
builder.Services.AddHealthChecks();

// Логирование HTTP-запросов: только метод/путь/код/длительность.
// НЕ логируем заголовки и тело — иначе в логи попадут токены и персональные данные.
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
                      | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
                      | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
                      | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
});

// Ограничение частоты запросов к аутентификации — защита от перебора паролей.
// Ключ — IP-адрес: не более 10 попыток в минуту на адрес.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

    // Пересоздание БД включается явным флагом, а не самим фактом dev-среды: иначе
    // каждый перезапуск API стирал заведённые документы и сессии, а на демонстрации
    // и при отладке данные должны переживать рестарт.
    // Включить: Database:RecreateOnStartup=true (env DATABASE__RECREATEONSTARTUP=true).
    var recreate = app.Configuration.GetValue<bool>("Database:RecreateOnStartup");

    if (recreate && app.Environment.IsDevelopment())
    {
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();

    // Стартовые инструкции заводятся один раз — когда статей нет вовсе. Дальше их
    // ведёт администратор, и перезаписывать его правки при каждом запуске нельзя:
    // инструкция принадлежит банку, а не сборке.
    delosfera_server.Modules.Help.Services.HelpSeeder.Seed(db);

    // Bootstrap администратора из конфигурации (env/secrets), а НЕ из захардкоженного хеша.
    // Пароли сид-аккаунтов инвалидированы миграцией InvalidateSeededPasswords; этот блок —
    // единственный способ выдать рабочий пароль администратору, без коммита хеша в репозиторий.
    // Учётные записи для обкатки бизнес-подразделениями. Заводятся только при явно
    // включённой настройке и только если задан пароль — в коде его нет и не будет.
    // На продуктивном контуре Demo:Enabled выключен, и этот блок не делает ничего.
    if (app.Configuration.GetValue<bool>("Demo:Enabled"))
    {
        delosfera_server.Modules.Users.Services.DemoAccountsSeeder.Seed(
            db,
            scope.ServiceProvider.GetRequiredService<IUserPasswordHasher>(),
            app.Configuration["Demo:Password"] ?? "",
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Demo"));
    }

    // Роли органов банка. Строго до раздачи прав: право, у которого нет
    // роли-держателя, закрывает действие для всех и выглядит при этом исправным.
    await delosfera_server.Modules.Users.Services.CoreRolesSeeder.ApplyAsync(
        db,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CoreRoles"));

    // Права на новые разделы существующим ролям. Без этого раздел после выкладки
    // не видит никто: право заведено, но ни одной роли не принадлежит.
    await delosfera_server.Modules.Users.Services.RolePermissionDefaults.ApplyAsync(
        db,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RoleDefaults"));

    // Разовая инициализация нумераторов из текущих максимумов — до первой выдачи
    // номера через INumeratorService, иначе номер начался бы с 1 и столкнулся с
    // существующими регистрационными номерами. Идемпотентно.
    await delosfera_server.Modules.Documents.Services.NumeratorSeeder.SeedAsync(db);

    // Глобальный шаблон маршрута закупки из прежней цепочки (единый конструктор
    // согласующих). Идемпотентно: если шаблон уже есть — не трогает.
    await delosfera_server.Modules.Procurement.Services.ProcurementRouteTemplateSeeder.SeedAsync(db);

    // Пароль администратору выдаётся только так — из настройки, не из хеша в коде.
    // Блок находит уже заведённую учётную запись и ставит ей пароль; новых он не
    // создаёт. Учётные записи приходят из справочника банка, и завести здесь ещё
    // одну означало бы человека без подразделения и должности.
    var adminEmail = app.Configuration["Bootstrap:AdminEmail"];
    var adminPassword = app.Configuration["Bootstrap:AdminPassword"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var bootstrapLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
        var hasher = scope.ServiceProvider.GetRequiredService<IUserPasswordHasher>();
        var admin = db.Users.FirstOrDefault(u => u.Email == adminEmail);

        if (admin is not null)
        {
            admin.PasswordHash = hasher.Hash(adminPassword);
            admin.IsActive = true;
            admin.BlockedAt = null;
            db.SaveChanges();

            bootstrapLog.LogInformation(
                "Пароль выдан учётной записи {Email}. Войти можно ею.", adminEmail);
        }
        else
        {
            // Молчать здесь нельзя. Развёртывание выглядело бы успешным, а войти
            // было бы нечем: приложение стартовало, health отвечает, и только на
            // экране входа выясняется, что пароля нет ни у кого. Причина — опечатка
            // в адресе или адрес, которого нет в справочнике.
            var known = db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Id)
                .Select(u => u.Email)
                .Take(5)
                .ToList();

            bootstrapLog.LogError(
                "Bootstrap:AdminEmail = «{Email}» — такой учётной записи нет, пароль выдать некому. " +
                "Блок меняет пароль существующей записи, а не создаёт новую. " +
                "Укажите адрес из справочника, например: {Known}. " +
                "Всего активных записей: {Count}.",
                adminEmail, string.Join(", ", known), db.Users.Count(u => u.IsActive));
        }
    }
}

// Создаём бакет MinIO при старте, если его ещё нет
using (var scope = app.Services.CreateScope())
{
    var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var bucket = app.Configuration["Minio:Bucket"]!;

    var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));
    if (!exists)
    {
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
    }
}

// Проверка среды, если режим = Development (из launchSettings.json)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Delosfera API";
        options.Theme = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Shell, ScalarClient.Curl);
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>(); // единая обработка ошибок, без утечки стектрейсов
app.UseHttpLogging();
// Заголовки безопасности ответов (NFR-03) — ставятся раньше всего, чтобы попасть
// и в ответы об ошибках, а не только в успешные.
app.UseMiddleware<SecurityHeadersMiddleware>();

// HSTS говорит браузеру ходить только по HTTPS. В разработке не включаем: там
// сертификата нет, и браузер запомнил бы недоступный адрес надолго.
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection(); // Перенаправляет все входящие HTTP-запросы на HTTPS
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous(); // liveness-проба, без авторизации
app.Run();