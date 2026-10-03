using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace CricketStream.Infrastructure.Persistence;

public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter() : base(v => v.ToUniversalTime(), v => v) { }
}

public sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTimeOffset?>
{
    public NullableUtcDateTimeOffsetConverter() : base(v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v) { }
}
