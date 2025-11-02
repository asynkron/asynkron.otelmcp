namespace TraceLens;

public static class Extensions
{
    public static DateTimeOffset RoundToNearestTimeSpan(this DateTimeOffset dateTime, TimeSpan bucketSize)
    {
        var totalSeconds = (int)dateTime.TimeOfDay.TotalSeconds;
        var roundedSeconds = (int)Math.Round(totalSeconds / bucketSize.TotalSeconds) * bucketSize.TotalSeconds;
        return new DateTimeOffset(dateTime.Date.AddSeconds(roundedSeconds), dateTime.Offset);
    }

    public static DateTimeOffset UnixNanosToDateTimeOffset(this ulong t)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds((long)(t / 1_000_000));
    }

    public static ulong ToUnixTimeNanoseconds(this DateTimeOffset t)
    {
        return (ulong)t.ToUnixTimeMilliseconds() * 1_000_000;
    }
}