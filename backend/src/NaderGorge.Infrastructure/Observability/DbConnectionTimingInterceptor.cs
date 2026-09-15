using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NaderGorge.Infrastructure.Observability;

public sealed class DbConnectionTimingInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => RequestDbCommandScope.RecordConnection(eventData.Duration);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RequestDbCommandScope.RecordConnection(eventData.Duration);
        return Task.CompletedTask;
    }
}
