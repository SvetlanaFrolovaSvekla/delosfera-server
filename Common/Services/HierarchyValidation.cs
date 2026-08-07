using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Models;

namespace delosfera_server.Common.Services;

/// <summary>
/// Общая валидация для иерархических справочников:
/// существование родителя, защита от циклических ссылок, ограничение глубины
/// вложенности. Формулировки ошибок остаются на стороне вызывающего сервиса.
/// </summary>
public static class HierarchyValidation
{
    public const int DefaultMaxDepth = 5; // Максимальная длина иерархии (кол-во узлов)

    public static async Task EnsureParentExistsAsync<T>(
        DbSet<T> set, int parentId, Func<int, string> notFoundMessage)
        where T : class, IHierarchicalEntity
    {
        var exists = await set.AnyAsync(x => x.Id == parentId);
        if (!exists)
            throw new KeyNotFoundException(notFoundMessage(parentId));
    }

    /// <summary>Идёт вверх по цепочке ParentId от newParentId и проверяет, что nodeId
    /// не встречается на пути — иначе выбор такого родителя создал бы цикл.</summary>
    public static async Task EnsureNoCircularReferenceAsync<T>(
        DbSet<T> set, int nodeId, int newParentId, string circularReferenceMessage)
        where T : class, IHierarchicalEntity
    {
        var currentId = (int?)newParentId;
        var visited = new HashSet<int>();

        while (currentId.HasValue)
        {
            if (currentId.Value == nodeId)
                throw new InvalidOperationException(circularReferenceMessage);

            if (!visited.Add(currentId.Value))
                break; // защита от зависания, если в данных уже случайно оказался цикл

            currentId = await set
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync();
        }
    }

    public static async Task EnsureDepthNotExceededAsync<T>(
        DbSet<T> set, int parentId, int maxDepth, Func<int, string> depthExceededMessage)
        where T : class, IHierarchicalEntity
    {
        var depth = 1;
        var currentId = (int?)parentId;

        while (currentId.HasValue)
        {
            depth++;
            if (depth > maxDepth)
                throw new InvalidOperationException(depthExceededMessage(maxDepth));

            currentId = await set
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync();
        }
    }
}