using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

public interface ITaskDelegationService
{
    /// <summary>Передать одну открытую задачу другому сотруднику (СК-3).</summary>
    Task DelegateAsync(int taskId, int toUserId, string? comment, int actorUserId);
}

/// <summary>
/// Делегирование задачи (СК-3): исполнитель разово передаёт одну свою задачу коллеге.
///
/// Отличается от замещения (GEN-14): замещение временное, охватывает все задачи
/// отсутствующего и снимается по датам; делегирование — точечная передача одной
/// задачи по решению самого исполнителя, без срока.
///
/// Пока делегируются только задачи согласования: у них исполнитель — участник
/// маршрута, и, сменив участника, мы делаем делегата тем, кто вправе поставить визу
/// (RouteEngine.ResolveAsync проверяет именно участника). У поручений и решений
/// адресата исполнитель определяется ролью в документе, а не задачей, и их передача —
/// отдельный разговор.
/// </summary>
public class TaskDelegationService : ITaskDelegationService
{
    private readonly DelosferaDbContext _db;
    private readonly ISubstitutionService _substitutions;
    private readonly IAuditService _audit;

    public TaskDelegationService(
        DelosferaDbContext db, ISubstitutionService substitutions, IAuditService audit)
    {
        _db = db;
        _substitutions = substitutions;
        _audit = audit;
    }

    public async Task DelegateAsync(int taskId, int toUserId, string? comment, int actorUserId)
    {
        var task = await _db.WorkflowTasks.FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new KeyNotFoundException("Задача не найдена");

        if (task.State != WorkflowTaskState.Open)
            throw new InvalidOperationException("Задачу нельзя делегировать: она уже закрыта");

        if (task.RouteParticipantId is not { } participantId)
            throw new InvalidOperationException(
                "Пока делегируются только задачи согласования");

        // Делегирует сам исполнитель либо тот, кто его сейчас замещает: иначе задачу
        // мог бы передать любой, и ответственность за визу размылась бы.
        if (task.AssigneeUserId != actorUserId)
        {
            var actingFor = await _substitutions.GetActingForUserIdsAsync(actorUserId);
            if (!actingFor.Contains(task.AssigneeUserId))
                throw new InvalidOperationException("Делегировать задачу может её исполнитель или его замещающий");
        }

        if (toUserId == task.AssigneeUserId)
            throw new InvalidOperationException("Задача уже назначена этому сотруднику");

        var target = await _db.Users
            .Where(u => u.Id == toUserId)
            .Select(u => new { u.Id, u.IsActive, u.FullName })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Сотрудник не найден");

        if (!target.IsActive)
            throw new InvalidOperationException("Нельзя делегировать задачу отключённому сотруднику");

        var participant = await _db.RouteParticipants.FirstOrDefaultAsync(p => p.Id == participantId)
            ?? throw new InvalidOperationException("Участник маршрута задачи не найден");

        var from = task.AssigneeUserId;

        // Согласующим этапа становится делегат — тогда движок примет его визу.
        participant.UserId = toUserId;
        task.DelegatedByUserId = from;
        task.AssigneeUserId = toUserId;

        await _audit.LogAsync("WorkflowTask", task.Id, "Delegated", actorUserId, new
        {
            from,
            to = toUserId,
            comment,
        });

        await _db.SaveChangesAsync();
    }
}
