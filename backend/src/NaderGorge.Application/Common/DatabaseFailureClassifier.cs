using System.Data.Common;

namespace NaderGorge.Application.Common;

public static class DatabaseFailureClassifier
{
    public static bool IsTransient(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true }) return true;
        }
        return false;
    }
}
