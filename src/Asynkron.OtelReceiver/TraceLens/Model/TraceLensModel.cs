namespace TraceLens.Model;

public record TraceLensModel
{
    private TraceLensModel(IList<Span> entries, bool diagnostics)
    {
        entries = entries.OrderBy(x => x.StartTimeUnixNano).ToList();
        var spansById = entries.ToLookup(e => e.SpanId);
        var spansByParentId = entries.ToLookup(e => e.ParentSpanId);
        
        //first pass
        foreach (var e in entries)
        {
            e.Children = spansByParentId[e.SpanId].ToArray();
            e.Parent = spansById[e.ParentSpanId].FirstOrDefault();
            //add the span log entry
        }

        //second pass. calculate depth, add diagnostics
        foreach (var e in entries)
        {
            if (diagnostics)
            {
                e.Logs.Add(new LogEntry(e.StartTimeUnixNano, "tag",
                    e.ServiceName + " " + e.OperationName + " " + e.SpanId,
                    e.Attributes, 0));
            }

            e.Depth = e.Parent?.Depth + 1 ?? 0;

            e.Logs = e.Logs.OrderBy(l => l.TimeUnixNano).ThenBy(l => l.SortOrder).ToList();

            foreach (var l in e.Logs)
            {
                l.Span = e;
            }
        }


        var allSpanIds = entries.Select(e => e.SpanId).ToHashSet();
        var rootSpanIds = entries.Where(e => e.ParentSpanId == "" || !allSpanIds.Contains(e.ParentSpanId))
            .Select(e => e.SpanId).ToHashSet();

        var rootSpans = entries.Where(e => rootSpanIds.Contains(e.SpanId)).ToArray();

        Root = new Span("root", "root", "Start", "d", 0, 0, new Dictionary<string, object?>(),
            [])
        {
            Children = rootSpans
        };
        
        foreach (var s in rootSpans) s.Parent = Root;

        //This touches .Description...
    }

    private Span Root { get; }


    public static TraceLensModel Create(IList<Span> entries, bool diagnostics)
    {
        return new TraceLensModel(entries, diagnostics);
    }
}