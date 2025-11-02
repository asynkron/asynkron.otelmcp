using System.Globalization;
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
}