using delosfera_server.Data;
using delosfera_server.Extensions;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Minio.DataModel.Args;

/*Создается построитель приложения, собирает настройки, переменные окружения*/
var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFileStorageService, MinioFileStorageService>();
builder.Services.AddScoped<IVndApprovalService, VndApprovalService>();
builder.Services.AddHostedService<VndApprovalTimeoutBackgroundService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITasksService, TasksService>();
builder.Services.AddScoped<ICoordinationDefaultApproverService, CoordinationDefaultApproverService>();
builder.Services.AddScoped<IVndActualizationService, VndActualizationService>();

builder.AddDatabase();
builder.AddDictionaryServices();
builder.AddVndServices();
builder.AddAnalyticsServices();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // В dev - полностью пересоздаём БД при каждом запуске
        // сносим всё и накатываем миграции заново
        db.Database.EnsureDeleted();
        db.Database.Migrate();
    }
    else
    {
        // В проде - только применяются новые миграции, ничего более не удаляется
        db.Database.Migrate();
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

app.UseHttpsRedirection(); // Перенаправляет все входящие HTTP-запросы на HTTPS
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();