using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter;

internal static class CodeFinanceTransaction
{
    public static Task<TeacherFinanceCommandResult> ExecuteAsync(IAppDbContext db,
        Func<CancellationToken, Task<TeacherFinanceCommandResult>> operation, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            try { return await operation(retryCt); }
            catch (Exception exception) when (SerializationRetryHelper.IsSerializationFailure(exception))
            {
                // Discard tracked changes from the rolled-back receipt before reading the remaining amount again.
                if (db is DbContext context) context.ChangeTracker.Clear();
                throw;
            }
        }, ct);
}
