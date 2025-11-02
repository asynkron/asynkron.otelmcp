namespace TraceLens.Model;


public record Span(string TraceId, string SpanId, string ParentSpanId, string ServiceName, string OperationName,
    ulong StartTimeUnixNano, ulong EndTimeUnixNano, Dictionary<string, object?> Attributes,
    List<LogEntry> Logs, OpenTelemetry.Proto.Trace.V1.Span.Types.SpanKind Kind)
{
    private bool? _hasError;

    private ulong? _totalSpanDuration;

    public Span[] Children { get; set; } = Array.Empty<Span>();
    public string ServiceName { get; } = ServiceName;

    public string OperationName { get; } = OperationName;
    public Dictionary<string, object?> Attributes { get; } = Attributes;
    public Span? Parent { get; set; }
    public List<LogEntry> Logs { get; set; } = Logs;

    public DateTimeOffset StartTime => StartTimeUnixNano.UnixNanosToDateTimeOffset();

    public DateTimeOffset EndTime => EndTimeUnixNano.UnixNanosToDateTimeOffset();

    public ulong DurationUnixNano => EndTimeUnixNano - StartTimeUnixNano;
    public ulong EndTimeUnixNano { get; set; } = EndTimeUnixNano;
    public int Depth { get; set; }

    public bool HasError
    {
        get
        {
            _hasError ??= Logs.Any(x => x.Level == LogEntryLevel.Error);
            return _hasError.Value;
        }
    }

    public string GetAttribute(string tag)
    {
        Attributes.TryGetValue(tag, out var x);
        return $"{x}";
    }

    public IEnumerable<Span> All()
    {
        var l = new List<Span>();

        void Add(Span self)
        {
            l.Add(self);
            foreach (var c in self.Children)
            {
                Add(c);
            }
        }

        Add(this);

        return l;
    }

    public ulong GetTotalSpanDurationRecursive()
    {
        if (_totalSpanDuration is not null) return _totalSpanDuration.Value;

        var widest = DurationUnixNano;
        var total = 0ul;
        foreach (var child in Children)
        {
            total += child.GetTotalSpanDurationRecursive();
        }

        if (total > widest) widest = total;

        _totalSpanDuration = widest;

        return widest;
    }
}