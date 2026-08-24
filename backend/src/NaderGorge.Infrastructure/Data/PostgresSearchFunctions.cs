using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Infrastructure.Data;

public static class PostgresSearchFunctions
{
    [DbFunction("massar_normalize_arabic")]
    public static string NormalizeArabic(string value) =>
        throw new InvalidOperationException("This function can only be evaluated by PostgreSQL.");
}
