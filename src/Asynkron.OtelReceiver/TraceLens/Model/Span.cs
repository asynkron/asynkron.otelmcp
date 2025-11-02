namespace TraceLens.Model;


public record Span(
    string SpanId, string ParentSpanId, string ServiceName, string OperationName,
    ulong StartTimeUnixNano, ulong EndTimeUnixNano, Dictionary<string, object?> Attributes,
    List<LogEntry> Logs)
{
    public Span[] Children { get; set; } = Array.Empty<Span>();
    public string ServiceName { get; } = ServiceName;

    public string OperationName { get; } = OperationName;
    public Dictionary<string, object?> Attributes { get; } = Attributes;
    public Span? Parent { get; set; }
    public List<LogEntry> Logs { get; set; } = Logs;

    public ulong EndTimeUnixNano { get; } = EndTimeUnixNano;
    public int Depth { get; set; }
}