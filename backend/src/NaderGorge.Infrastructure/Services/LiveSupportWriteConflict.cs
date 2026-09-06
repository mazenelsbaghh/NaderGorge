using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NaderGorge.Infrastructure.Services;

internal static class LiveSupportWriteConflict
{
    public static bool IsRetryable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is not PostgresException postgres) continue;
            if (postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                return true;
            if (postgres.SqlState == PostgresErrorCodes.UniqueViolation && postgres.ConstraintName is
                "IX_live_support_events_ConversationId_Sequence" or
                "IX_live_support_whatsapp_pending_receipts_MetaMessageId")
                return true;
        }
        return false;
    }
}
