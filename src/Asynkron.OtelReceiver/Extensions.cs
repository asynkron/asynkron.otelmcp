using System.Globalization;
using System.Threading.Channels;
using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace TraceLens.Infra;

public static class Extensions
{
    public static string GetServiceName(this ResourceSpans resourceSpan)
    {
        return resourceSpan.Resource.Attributes.First(y => y.Key == "service.name").Value.StringValue;
    }

    public static string ToStringValue(this AnyValue value)
    {
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.None => "",
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.ArrayValue => value.ArrayValue.ToString(),
            AnyValue.ValueOneofCase.KvlistValue => value.KvlistValue.ToString(),
            AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToString(),
            _ => throw new ArgumentOutOfRangeException()
        } ?? string.Empty;
    }


    public static string ToHex(this ByteString bytes)
    {
        var hex = BitConverter.ToString(bytes.ToByteArray()).Replace("-", "");
        return hex;
    }
    
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
    
    
    public static async ValueTask<List<T>> ReadBatchAsync<T>(this ChannelReader<T> reader, int max)
    {
        await reader.WaitToReadAsync();
        var results = new List<T>(max);
        var i = 3;
        while (true)
        {
            while (
                results.Count < max
                && reader.TryRead(out var item))
                results.Add(item);

            if (results.Count == max)
                return results;

            if (i > 0)
            {
                await Task.Delay(100);
                i--;
            }
            else
            {
                return results;
            }
        }
    }
}