using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeamFinder.Api.Data;

namespace TeamFinder.Api.Common;

public static class ProjectLock
{
    /// <summary>
    /// Открывает транзакцию и блокирует строку проекта (SELECT ... FOR UPDATE).
    ///
    /// Все операции, которые меняют заявки, участников, роли или состояние одного проекта,
    /// начинаются с этой блокировки. Поэтому они выполняются по очереди: два одновременных
    /// принятия на последнее место, принятие и перевод проекта в работу, отзыв и принятие одной
    /// заявки не могут «проскочить» друг мимо друга (RULE-CAP-03, NFR-02).
    /// Данные проекта читаются уже после получения блокировки, то есть всегда актуальные.
    /// </summary>
    public static async Task<IDbContextTransaction> BeginProjectLockAsync(
        this TeamFinderDbContext db, long projectId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM projects WHERE \"Id\" = {projectId} FOR UPDATE", ct);
        return transaction;
    }
}
