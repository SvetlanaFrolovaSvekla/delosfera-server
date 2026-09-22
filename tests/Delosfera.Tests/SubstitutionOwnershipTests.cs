using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Models;
using delosfera_server.Modules.Substitutions.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Контроль доступа к заявкам на замещение (КСЗ-В9).
///
/// Сервис грузил заявку по {id} и проверял только СТАТУС: любой аутентифицированный
/// мог отредактировать, отправить, отозвать или удалить чужую заявку (IDOR — паспорт,
/// ИНН, адреса замещающего). Проверяем, что мутации теперь доступны только инициатору
/// (или привилегированному пользователю), а посторонний получает отказ в доступе.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SubstitutionOwnershipTests
{
    private readonly PostgresFixture _postgres;

    public SubstitutionOwnershipTests(PostgresFixture postgres) => _postgres = postgres;

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Инициатор_редактирует_свою_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        var обновлено = await сервис.UpdateAsync(id, Черновик("Уточнённая тема"), инициатор);

        Assert.Equal("Уточнённая тема", обновлено.Subject);
    }

    [Fact]
    public async Task Чужую_заявку_редактировать_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var посторонний = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.UpdateAsync(id, Черновик("Подмена"), посторонний));
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Инициатор_отправляет_свою_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        var отправлено = await сервис.SubmitAsync(id, инициатор);

        Assert.NotEqual(nameof(SubstitutionStatus.Draft), отправлено.Status);
        Assert.False(string.IsNullOrWhiteSpace(отправлено.RegNumber));
    }

    [Fact]
    public async Task Чужую_заявку_отправить_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var посторонний = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.SubmitAsync(id, посторонний));

        // Рег.номер не присвоен — заявка осталась черновиком.
        var заявка = await db.SubstitutionRequests.AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.Equal(SubstitutionStatus.Draft, заявка.Status);
        Assert.Null(заявка.RegNumber);
    }

    // ── Withdraw ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Инициатор_отзывает_свою_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;
        await сервис.SubmitAsync(id, инициатор);

        var отозвано = await сервис.WithdrawAsync(id, инициатор);

        Assert.Equal(nameof(SubstitutionStatus.Withdrawn), отозвано.Status);
    }

    [Fact]
    public async Task Чужую_заявку_отозвать_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var посторонний = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;
        await сервис.SubmitAsync(id, инициатор);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.WithdrawAsync(id, посторонний));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Инициатор_удаляет_свой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        await сервис.DeleteAsync(id, инициатор);

        Assert.False(await db.SubstitutionRequests.AnyAsync(x => x.Id == id));
    }

    [Fact]
    public async Task Чужой_черновик_удалить_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);
        var инициатор = await ПользовательАsync(db);
        var посторонний = await ПользовательАsync(db);
        var id = (await сервис.CreateAsync(Черновик(), инициатор)).Id;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.DeleteAsync(id, посторонний));

        // Черновик на месте — посторонний ничего не удалил.
        Assert.True(await db.SubstitutionRequests.AnyAsync(x => x.Id == id));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static SubstitutionService Сервис(DelosferaDbContext db) =>
        new(db, new AuditService(db), new NumeratorService(db), new BankClock(), new NoopPrint());

    private static SubstitutionSaveRequest Черновик(string subject = "Замещение на время отпуска") => new()
    {
        Subject = subject,
        Reason = SubstitutionReason.Vacation,
        AbsentName = "Отсутствующий сотрудник",
        SubstituteName = "Замещающий сотрудник",
        StartsOn = new DateOnly(2026, 10, 1),
        EndsOn = new DateOnly(2026, 10, 14),
    };

    private static async Task<int> ПользовательАsync(DelosferaDbContext db)
    {
        var user = new User
        {
            FullName = "Сотрудник",
            Email = $"sub-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Печать в этих проверках не задействована — важен контроль доступа.</summary>
    private sealed class NoopPrint : ISubstitutionPrintService
    {
        public byte[] Order(SubstitutionRequest r) => [];
        public byte[] Liability(SubstitutionRequest r) => [];
        public byte[] Card(SubstitutionRequest r) => [];
    }
}
