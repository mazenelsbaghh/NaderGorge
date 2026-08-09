using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NaderGorge.Infrastructure.Data;

internal static class UtcDateTimeModelConvention
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeConverter = new(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeConverter = new(
        value => value.HasValue
            ? value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(DateTimeConverter);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(NullableDateTimeConverter);
        }
    }
}
