using System.Text;
using System.Text.Json;
using Hocon;
using Hocon.Json;

namespace TraceLens.Model;

public class AttributeString
{
    private static readonly UTF8Encoding Utf8 = new(true, true);

    public AttributeString(string value, bool magic)
    {
        Value = value;
        Evaluate();
    }

    public string? Decoded { get; set; }

    public byte[]? Bytes { get; set; }

    public string Value { get; }

    public bool IsJson => Json != null;
    public bool IsBytes => Bytes != null;

    public bool IsDecoded => Decoded != null;

    public string? Json { get; set; }

    private void Evaluate()
    {
        if (Value.Length < 10) return;
        ParseJson(Value);
        ParseBase64();
        if (!IsBytes) return;
        Decoded = DecodeBytes();
        if (Decoded != null) ParseJson(Decoded);
    }

    private string? DecodeBytes()
    {
        try
        {
            var res = Utf8.GetString(Bytes!);
            return res;
        }
        catch (Exception)
        {
        }

        return null;
    }

    private void ParseBase64()
    {
        var value = Value;
        try
        {
            Bytes = Convert.FromBase64String(value);
        }
        catch
        {
        }
    }

    public override string ToString()
    {
        if (IsJson) return Json!;
        if (IsDecoded) return Decoded!;
        return Value;
    }


    private void ParseJson(string value)
    {
        var json = value;
        json = json.Trim();
        if (!(json.EndsWith("}") || json.EndsWith("]")))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                Json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }

            return;
        }
        catch
        {
        }

        try
        {
            var root = HoconParser.Parse(json).ToJToken();
            var maybe = root!.ToString();
            var doc = JsonDocument.Parse(maybe);
            if (doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                Json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch
        {
        }
    }
}