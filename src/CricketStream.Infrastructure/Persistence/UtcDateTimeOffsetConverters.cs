using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace CricketStream.Infrastructure.Persistence;

/// <summary>SQLite cannot sort or compare DateTimeOffset values, so every one is stored as UTC ticks (a number). That also normalises clients that send +05:30 and so on.</summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, long>
{
    public UtcDateTimeOffsetConverter() : base(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)) { }
}

public sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, long?>
{
    public NullableUtcDateTimeOffsetConverter() : base(
        v => v.HasValue ? v.Value.UtcTicks : null,
        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null) { }
}
