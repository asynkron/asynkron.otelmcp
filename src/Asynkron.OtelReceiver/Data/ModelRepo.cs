using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Proto.Logs.V1;
using TraceLens.Infra;
using OtelMcp.Proto.V1;
using Metric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Asynkron.OtelReceiver.Data;

public class ModelRepo(
    IDbContextFactory<OtelReceiverContext> contextFactory,
    ILogger<ModelRepo> logger,
    ISpanBulkInserter spanBulkInserter,
    IReceiverMetricsCollector metricsCollector)
{
    private static readonly HashSet<string> BlockedAttributes =
    [
        "proto.actorpid",
        "proto.senderpid",
        "proto.targetpid"
    ];

    private static readonly HashSet<SpanAttributeEntity> SeenAttributes = [];
    private static readonly HashSet<SpanNameEntity> SeenSpanNames = [];

    public async Task SaveTrace(SpanWithService[] chunk)
    {
        logger.LogInformation("Before save spans");
        var spanNames = new HashSet<SpanNameEntity>();
        var attributeLookups = new HashSet<SpanAttributeEntity>();
        var spanAttributeRows = new List<SpanAttributeValueEntity>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var spans = new List<SpanEntity>();
        foreach (var s in chunk)
        {
            var span = new SpanEntity
            {
                TraceId = s.Span.TraceId.ToHex(),
                SpanId = s.Span.SpanId.ToHex(),
                ParentSpanId = s.Span.ParentSpanId.ToHex(),
                ServiceName = s.ServiceName,
                OperationName = s.Span.Name,
                StartTimestamp = (long)s.Span.StartTimeUnixNano,
                EndTimestamp = (long)s.Span.EndTimeUnixNano,
                AttributeMap = s.Span.Attributes
                    .Select(kvp => $"{kvp.Key}:{kvp.Value.ToStringValue()}").ToArray(),
                Events = s.Span.Events.Select(e => e.Name).ToArray(),
                Proto = s.ToByteArray()
            };
            spans.Add(span);

            foreach (var a in s.Span.Attributes)
            {
                if (BlockedAttributes.Contains(a.Key)) continue;

                var attributeValue = a.Value.ToStringValue();

                spanAttributeRows.Add(new SpanAttributeValueEntity
                {
                    SpanId = span.SpanId,
                    Key = a.Key,
                    Value = attributeValue,
                    Source = SpanAttributeSource.Span
                });

                var spanAttrib = new SpanAttributeEntity
                {
                    Key = a.Key,
                    Value = attributeValue
                };
                if (SeenAttributes.Add(spanAttrib)) attributeLookups.Add(spanAttrib);
            }

            var spanName = new SpanNameEntity
            {
                ServiceName = s.ServiceName,
                Name = s.Span.Name
            };

            if (SeenSpanNames.Add(spanName)) spanNames.Add(spanName);
        }

        try
        {
            await spanBulkInserter.InsertAsync(context, spans, spanAttributeRows, CancellationToken.None);
            logger.LogInformation("Before save changes");
            await context.SaveChangesAsync();
            logger.LogInformation("After save changes");
            metricsCollector.RecordSpansStored(spans.Count);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Failed to write spans");
        }

        logger.LogInformation("After save spans");

        logger.LogInformation("Before save attributes");
        foreach (var attrib in attributeLookups)
        {
            try
            {
                //insert using raw sql instead
                await context.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "SpanAttributes" ("Key", "Value") VALUES (@p0, @p1) ON CONFLICT DO NOTHING""",
                    attrib.Key, attrib.Value);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Failed to write attributes");
            }
        }

        logger.LogInformation("After save attributes");

        logger.LogInformation("Before save span names");
        foreach (var spanName in spanNames)
        {
            try
            {
                //insert using raw sql instead
                await context.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "SpanNames" ("ServiceName", "Name") VALUES (@p0, @p1) ON CONFLICT DO NOTHING""",
                    spanName.ServiceName, spanName.Name);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Failed to write span names");
            }
        }

        logger.LogInformation("After save span names");
    }

    public async Task SaveLogs((LogRecord log, ResourceLogs resourceLog)[] chunk)
    {
        logger.LogInformation("Starting to save logs");
        await using var context = await contextFactory.CreateDbContextAsync();
        var logs = new List<LogEntity>();
        foreach (var t in chunk)
        {
            var l = t.log;

            //TODO: add formatted body
            var r = t.resourceLog;
            var attributes = new List<LogAttributeEntity>();

            foreach (var attribute in l.Attributes)
            {
                attributes.Add(new LogAttributeEntity
                {
                    Key = attribute.Key,
                    Value = attribute.Value.ToStringValue(),
                    Source = LogAttributeSource.Record
                });
            }

            foreach (var attribute in r.Resource.Attributes)
            {
                attributes.Add(new LogAttributeEntity
                {
                    Key = attribute.Key,
                    Value = attribute.Value.ToStringValue(),
                    Source = LogAttributeSource.Resource
                });
            }

            var log = new LogEntity
            {
                TraceId = l.TraceId.ToHex(),
                SpanId = l.SpanId.ToHex(),
                Timestamp = (long)l.TimeUnixNano,
                ObservedTimestamp = (long)l.ObservedTimeUnixNano,
                SeverityText = l.SeverityText,
                SeverityNumber = (byte)l.SeverityNumber,
                Body = FormatLog(l),
                RawBody = l.Body.StringValue,
                Proto = l.ToByteArray(),
                Attributes = attributes
            };
            logs.Add(log);
        }

        try
        {
            logger.LogInformation("Before inserting logs");
            await context.Logs.AddRangeAsync(logs);
            await context.SaveChangesAsync();
            logger.LogInformation("After inserting logs");
            metricsCollector.RecordLogsStored(logs.Count);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Failed to write logs");
        }

        logger.LogInformation("Done saving logs");
    }

    private static string FormatLog(LogRecord l)
    {
        var str = l.Body.StringValue;
        if (!string.IsNullOrEmpty(str))
            return str;

        var attributes = l.Attributes.ToDictionary(
            x => x.Key,
            x => x.Value.ToStringValue());

        // Joining attributes makes it easier to inspect ad-hoc log payloads when
        // the body field is empty. This keeps the previous "key:value" format.
        return string.Join(", ", attributes.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }

    public async Task<GetSearchDataResponse> GetSearchData(GetSearchDataRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var serviceNames = await context.Spans
            .AsNoTracking()
            .Select(span => span.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var spanNames = await context.SpanNames
            .AsNoTracking()
            .Select(span => span.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var tagNames = await context.SpanAttributes
            .AsNoTracking()
            .Select(attribute => attribute.Key)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var response = new GetSearchDataResponse();
        response.ServiceNames.AddRange(serviceNames);
        response.SpanNames.AddRange(spanNames);
        response.TagNames.AddRange(tagNames);

        return response;
    }

    public async Task<GetValuesForTagResponse> GetValuesForTag(GetValuesForTagRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var values = await context.SpanAttributes
            .AsNoTracking()
            .Where(attribute => attribute.Key == request.TagName)
            .Select(attribute => attribute.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync();

        var response = new GetValuesForTagResponse();
        response.TagValues.AddRange(values);

        return response;
    }
    
    public async Task SaveMetrics(MetricEntity[] chunk)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Metrics.AddRangeAsync(chunk);
        await context.SaveChangesAsync();
        logger.LogInformation("Saving metrics {Size}", chunk.Length);
        metricsCollector.RecordMetricsStored(chunk.Length);
    }

    public async Task<GetMetricNamesResponse> GetMetricNames()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var names = await context.Metrics
            .AsNoTracking()
            .Select(m => m.Name)
            .Distinct()
            .ToListAsync();

        return new GetMetricNamesResponse
        {
            Name = { names }
        };
    }

    public async Task<GetMetricResponse> GetMetric(GetMetricRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var data = await context.Metrics
            .AsNoTracking()
            .Where(m => m.Name == request.Name)
            .ToListAsync();

        var protos = data.Select(m => Metric.Parser.ParseFrom(m.Proto)).ToList();

        return new GetMetricResponse
        {
            Metrics = { protos }
        };
    }

    public async Task<GetTraceResponse> GetTrace(GetTraceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TraceId))
        {
            return new GetTraceResponse();
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        var spansTask = context.Spans
            .AsNoTracking()
            .Where(span => span.TraceId == request.TraceId)
            .ToListAsync();

        var logsTask = context.Logs
            .AsNoTracking()
            .Where(log => log.TraceId == request.TraceId)
            .ToListAsync();

        await Task.WhenAll(spansTask, logsTask);

        var spans = await spansTask;
        var logs = await logsTask;

        if (spans.Count == 0)
        {
            return new GetTraceResponse();
        }

        var response = new GetTraceResponse();
        PopulateTraceResponse(spans, logs, response.Spans, response.Logs);
        return response;
    }

    public async Task<GetRandomTraceResponse> GetRandomTrace(GetRandomTraceRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var randomTraceId = await context.Spans
            .AsNoTracking()
            .Select(span => span.TraceId)
            .FirstOrDefaultAsync();

        if (randomTraceId is null)
        {
            return new GetRandomTraceResponse();
        }

        var spansTask = context.Spans
            .AsNoTracking()
            .Where(span => span.TraceId == randomTraceId)
            .ToListAsync();

        var logsTask = context.Logs
            .AsNoTracking()
            .Where(log => log.TraceId == randomTraceId)
            .ToListAsync();

        await Task.WhenAll(spansTask, logsTask);

        var spans = await spansTask;
        var logs = await logsTask;

        var response = new GetRandomTraceResponse();
        PopulateTraceResponse(spans, logs, response.Spans, response.Logs);
        return response;
    }

    private static void PopulateTraceResponse(
        List<SpanEntity> spans,
        List<LogEntity> logs,
        Google.Protobuf.Collections.RepeatedField<SpanWithService> spansField,
        Google.Protobuf.Collections.RepeatedField<LogRecord> logsField)
    {
        foreach (var span in spans)
        {
            var spanProto = SpanWithService.Parser.ParseFrom(span.Proto);
            spansField.Add(spanProto);
        }

        foreach (var log in logs)
        {
            var logRecord = LogRecord.Parser.ParseFrom(log.Proto);
            logsField.Add(logRecord);
        }
    }

    public async Task<SearchTraceResponse> SearchTraces(SearchTracesRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var serviceNames = new HashSet<string>();
        var spanNames = new HashSet<string>();
        CollectFilterHints(request.Filter, serviceNames, spanNames);

        var requiredSpanAttributeFilters = new List<AttributeFilter>();
        CollectRequiredSpanAttributeFilters(request.Filter, requiredSpanAttributeFilters, true);

        var requiredLogAttributeFilters = new List<AttributeFilter>();
        CollectRequiredLogAttributeFilters(request.Filter, requiredLogAttributeFilters, true);

        var (minDuration, maxDuration) = CollectDurationBounds(request.Filter);

        var spanQuery = context.Spans.AsNoTracking();
        var attributeQuery = context.SpanAttributeValues.AsNoTracking();

        if (serviceNames.Count > 0)
            spanQuery = spanQuery.Where(span => serviceNames.Contains(span.ServiceName));

        if (spanNames.Count > 0)
            spanQuery = spanQuery.Where(span => spanNames.Contains(span.OperationName));

        foreach (var filter in requiredSpanAttributeFilters)
            spanQuery = ApplySpanAttributeFilter(spanQuery, attributeQuery, filter);

        if (minDuration.HasValue || maxDuration.HasValue)
        {
            var minDurationLong = minDuration.HasValue ? (long)minDuration.Value : long.MinValue;
            var maxDurationLong = maxDuration.HasValue ? (long)maxDuration.Value : long.MaxValue;
            
            if (minDuration.HasValue && maxDuration.HasValue)
                spanQuery = spanQuery.Where(span =>
                    (span.EndTimestamp - span.StartTimestamp) >= minDurationLong &&
                    (span.EndTimestamp - span.StartTimestamp) <= maxDurationLong);
            else if (minDuration.HasValue)
                spanQuery = spanQuery.Where(span =>
                    (span.EndTimestamp - span.StartTimestamp) >= minDurationLong);
            else if (maxDuration.HasValue)
                spanQuery = spanQuery.Where(span =>
                    (span.EndTimestamp - span.StartTimestamp) <= maxDurationLong);
        }

        if (request.StartTime > 0)
            spanQuery = spanQuery.Where(span => (ulong)span.StartTimestamp >= request.StartTime);

        if (request.EndTime > 0)
            spanQuery = spanQuery.Where(span => (ulong)span.EndTimestamp <= request.EndTime);

        var traceIds = await spanQuery
            .Select(span => span.TraceId)
            .Distinct()
            .Take(request.Limit > 0 ? request.Limit : 100)
            .ToListAsync();

        var allSpans = await context.Spans
            .AsNoTracking()
            .Where(span => traceIds.Contains(span.TraceId))
            .ToListAsync();

        var allLogs = await context.Logs
            .AsNoTracking()
            .Include(log => log.Attributes)
            .Where(log => traceIds.Contains(log.TraceId))
            .ToListAsync();

        var allSpanAttributes = await context.SpanAttributeValues
            .AsNoTracking()
            .Where(attribute => allSpans.Select(s => s.SpanId).Contains(attribute.SpanId))
            .ToListAsync();

        var spanAttributesLookup = allSpanAttributes
            .GroupBy(attribute => attribute.SpanId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SpanAttributeValueEntity>)group.ToList());

        var spansByTrace = allSpans.GroupBy(span => span.TraceId);
        var logsByTrace = allLogs.GroupBy(log => log.TraceId);

        var results = new List<SearchTraceResult>();

        foreach (var traceGroup in spansByTrace)
        {
            var traceId = traceGroup.Key;
            var spans = traceGroup.ToList();
            var logs = logsByTrace.FirstOrDefault(g => g.Key == traceId)?.ToList() ?? new List<LogEntity>();

            var traceContext = new TraceContext(spans, logs, spanAttributesLookup);
            var clauseMap = new Dictionary<string, AttributeClauseMatch>();

            if (!EvaluateTraceFilter(request.Filter, traceContext, clauseMap))
                continue;

            var logQuery = logs.AsQueryable();
            foreach (var filter in requiredLogAttributeFilters)
                logQuery = ApplyLogAttributeFilter(logQuery, filter);

            var filteredLogs = logQuery.ToList();

            if (request.LogFilter != null && !string.IsNullOrWhiteSpace(request.LogFilter.BodyContains))
            {
                filteredLogs = filteredLogs
                    .Where(log => log.Body?.Contains(request.LogFilter.BodyContains, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            var startTime = spans.Min(s => (ulong)s.StartTimestamp);
            var endTime = spans.Max(s => (ulong)s.EndTimestamp);
            var hasError = spans.Any(SpanHasError);

            var traceOverview = new TraceOverview
            {
                TraceId = traceId,
                Name = spans.FirstOrDefault()?.OperationName ?? string.Empty,
                StartTimeUnixNano = startTime,
                EndTimeUnixNano = endTime,
                HasError = hasError
            };

            foreach (var span in spans)
            {
                traceOverview.Spans.Add(new SpanOverview
                {
                    TraceId = span.TraceId,
                    OperationName = span.OperationName,
                    ServiceName = span.ServiceName
                });
            }

            var result = new SearchTraceResult
            {
                Trace = traceOverview
            };

            result.AttributeClauses.AddRange(clauseMap.Values);

            foreach (var log in filteredLogs)
            {
                var logRecord = LogRecord.Parser.ParseFrom(log.Proto);
                result.Logs.Add(logRecord);
            }

            foreach (var span in spans)
            {
                var spanProto = SpanWithService.Parser.ParseFrom(span.Proto);
                result.Spans.Add(spanProto.Span);
            }

            results.Add(result);
        }

        var logCounts = allLogs
            .GroupBy(log => log.RawBody)
            .Select(group => new LogCount
            {
                RawBody = group.Key ?? string.Empty,
                Count = group.Count()
            })
            .ToList();

        var spanCounts = allSpans
            .GroupBy(span => span.OperationName)
            .Select(group => new SpanCount
            {
                SpanName = group.Key,
                Count = group.Count()
            })
            .ToList();

        var response = new SearchTraceResponse();
        response.Results.AddRange(results);
        response.LogCounts.AddRange(logCounts);
        response.SpanCounts.AddRange(spanCounts);

        return response;
    }

    private static void CollectFilterHints(
        TraceFilterExpression? expression,
        ISet<string> serviceNames,
        ISet<string> spanNames)
    {
        if (expression is null) return;

        switch (expression.ExpressionCase)
        {
            case TraceFilterExpression.ExpressionOneofCase.Service when
                !string.IsNullOrWhiteSpace(expression.Service.Name):
                serviceNames.Add(expression.Service.Name);
                break;
            case TraceFilterExpression.ExpressionOneofCase.SpanName when
                !string.IsNullOrWhiteSpace(expression.SpanName.Name):
                spanNames.Add(expression.SpanName.Name);
                break;
            case TraceFilterExpression.ExpressionOneofCase.Composite:
                foreach (var child in expression.Composite.Expressions)
                {
                    CollectFilterHints(child, serviceNames, spanNames);
                }

                break;
        }
    }

    private static (ulong? Min, ulong? Max) CollectDurationBounds(TraceFilterExpression? expression)
    {
        return CollectDurationBounds(expression, true);
    }

    private static (ulong? Min, ulong? Max) CollectDurationBounds(
        TraceFilterExpression? expression,
        bool allowHints)
    {
        if (!allowHints || expression is null) return (null, null);

        return expression.ExpressionCase switch
        {
            TraceFilterExpression.ExpressionOneofCase.Duration => NormalizeDurationBounds(expression.Duration),
            TraceFilterExpression.ExpressionOneofCase.Composite => CollectCompositeDurationBounds(expression.Composite),
            _ => (null, null)
        };
    }

    private static (ulong? Min, ulong? Max) CollectCompositeDurationBounds(TraceFilterComposite? composite)
    {
        if (composite is null || composite.Expressions.Count == 0) return (null, null);

        if (composite.Operator == TraceFilterComposite.Types.Operator.Or) return (null, null);

        ulong? min = null;
        ulong? max = null;

        foreach (var child in composite.Expressions)
        {
            var (childMin, childMax) = CollectDurationBounds(child, true);
            if (childMin.HasValue) min = min.HasValue ? Math.Max(min.Value, childMin.Value) : childMin.Value;

            if (childMax.HasValue) max = max.HasValue ? Math.Min(max.Value, childMax.Value) : childMax.Value;
        }

        return (min, max);
    }

    private static void CollectRequiredLogAttributeFilters(
        TraceFilterExpression? expression,
        ICollection<AttributeFilter> filters,
        bool isRequired)
    {
        if (expression is null ||
            (!isRequired && expression.ExpressionCase != TraceFilterExpression.ExpressionOneofCase.Composite)) return;

        switch (expression.ExpressionCase)
        {
            case TraceFilterExpression.ExpressionOneofCase.Attribute when
                isRequired &&
                expression.Attribute is { Target: AttributeFilterTarget.Log } attribute &&
                !string.IsNullOrWhiteSpace(attribute.Key):
                filters.Add(attribute);
                break;
            case TraceFilterExpression.ExpressionOneofCase.Composite:
                var composite = expression.Composite;
                if (composite is null || composite.Expressions.Count == 0) return;

                var isOr = composite.Operator == TraceFilterComposite.Types.Operator.Or;
                foreach (var child in composite.Expressions)
                {
                    CollectRequiredLogAttributeFilters(child, filters, isRequired && !isOr);
                }

                break;
        }
    }

    private static void CollectRequiredSpanAttributeFilters(
        TraceFilterExpression? expression,
        ICollection<AttributeFilter> filters,
        bool isRequired)
    {
        if (expression is null ||
            (!isRequired && expression.ExpressionCase != TraceFilterExpression.ExpressionOneofCase.Composite)) return;

        switch (expression.ExpressionCase)
        {
            case TraceFilterExpression.ExpressionOneofCase.Attribute when
                isRequired &&
                expression.Attribute is { } attribute &&
                attribute.Target != AttributeFilterTarget.Log &&
                !string.IsNullOrWhiteSpace(attribute.Key):
                filters.Add(attribute);
                break;
            case TraceFilterExpression.ExpressionOneofCase.Composite:
                var composite = expression.Composite;
                if (composite is null || composite.Expressions.Count == 0) return;

                var isOr = composite.Operator == TraceFilterComposite.Types.Operator.Or;
                foreach (var child in composite.Expressions)
                {
                    CollectRequiredSpanAttributeFilters(child, filters, isRequired && !isOr);
                }

                break;
        }
    }

    // Applies log attribute filters directly to SQL queries using EF Core query translation.
    // CompareTo is used for comparison operators as it translates to SQL comparison operators.
    // SQLite uses binary (ordinal) collation by default, matching the ordinal comparison used in in-memory evaluation.
    private static IQueryable<LogEntity> ApplyLogAttributeFilter(
        IQueryable<LogEntity> query,
        AttributeFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Key)) return query;

        var operation = NormalizeAttributeFilterOperator(filter);
        var key = filter.Key;
        var value = filter.Value;

        return operation switch
        {
            AttributeFilterOperator.Equals => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key && attribute.Value == value)),
            AttributeFilterOperator.NotEquals => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key && attribute.Value != value)),
            AttributeFilterOperator.Contains => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key && attribute.Value.Contains(value))),
            AttributeFilterOperator.GreaterThan => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key &&
                attribute.Value.CompareTo(value) > 0)),
            AttributeFilterOperator.LessThan => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key &&
                attribute.Value.CompareTo(value) < 0)),
            AttributeFilterOperator.GreaterThanOrEqual => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key &&
                attribute.Value.CompareTo(value) >= 0)),
            AttributeFilterOperator.LessThanOrEqual => query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == key &&
                attribute.Value.CompareTo(value) <= 0)),
            _ => query.Where(log => log.Attributes.Any(attribute => attribute.Key == key))
        };
    }

    // Applies span attribute filters directly to SQL queries using EF Core query translation.
    // CompareTo is used for comparison operators as it translates to SQL comparison operators.
    // SQLite uses binary (ordinal) collation by default, matching the ordinal comparison used in in-memory evaluation.
    private static IQueryable<SpanEntity> ApplySpanAttributeFilter(
        IQueryable<SpanEntity> query,
        IQueryable<SpanAttributeValueEntity> attributes,
        AttributeFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Key)) return query;

        var operation = NormalizeAttributeFilterOperator(filter);
        var key = filter.Key;
        var value = filter.Value;

        return operation switch
        {
            AttributeFilterOperator.Equals => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value == value)),
            AttributeFilterOperator.NotEquals => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value != value)),
            AttributeFilterOperator.Contains => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value.Contains(value))),
            AttributeFilterOperator.GreaterThan => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value.CompareTo(value) > 0)),
            AttributeFilterOperator.LessThan => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value.CompareTo(value) < 0)),
            AttributeFilterOperator.GreaterThanOrEqual => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value.CompareTo(value) >= 0)),
            AttributeFilterOperator.LessThanOrEqual => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key &&
                attribute.Value.CompareTo(value) <= 0)),
            _ => query.Where(span => attributes.Any(attribute =>
                attribute.SpanId == span.SpanId &&
                attribute.Key == key))
        };
    }

    private static AttributeFilterOperator NormalizeAttributeFilterOperator(AttributeFilter filter)
    {
        if (filter is null) return AttributeFilterOperator.Unspecified;

        return filter.Operator switch
        {
            AttributeFilterOperator.Equals => AttributeFilterOperator.Equals,
            AttributeFilterOperator.Exists => AttributeFilterOperator.Exists,
            AttributeFilterOperator.Contains => AttributeFilterOperator.Contains,
            AttributeFilterOperator.NotEquals => AttributeFilterOperator.NotEquals,
            AttributeFilterOperator.GreaterThan => AttributeFilterOperator.GreaterThan,
            AttributeFilterOperator.LessThan => AttributeFilterOperator.LessThan,
            AttributeFilterOperator.GreaterThanOrEqual => AttributeFilterOperator.GreaterThanOrEqual,
            AttributeFilterOperator.LessThanOrEqual => AttributeFilterOperator.LessThanOrEqual,
            AttributeFilterOperator.Unspecified => string.IsNullOrEmpty(filter.Value)
                ? AttributeFilterOperator.Exists
                : AttributeFilterOperator.Equals,
            _ => string.IsNullOrEmpty(filter.Value)
                ? AttributeFilterOperator.Exists
                : AttributeFilterOperator.Equals
        };
    }

    private static bool EvaluateTraceFilter(
        TraceFilterExpression? expression,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (expression is null) return true;

        return expression.ExpressionCase switch
        {
            TraceFilterExpression.ExpressionOneofCase.Attribute =>
                EvaluateAttributeFilter(expression.Attribute, traceContext, clauseMap),
            TraceFilterExpression.ExpressionOneofCase.Service =>
                EvaluateServiceFilter(expression.Service, traceContext),
            TraceFilterExpression.ExpressionOneofCase.SpanName =>
                EvaluateSpanNameFilter(expression.SpanName, traceContext),
            TraceFilterExpression.ExpressionOneofCase.Error =>
                EvaluateErrorFilter(expression.Error, traceContext),
            TraceFilterExpression.ExpressionOneofCase.Duration =>
                EvaluateDurationFilter(expression.Duration, traceContext),
            TraceFilterExpression.ExpressionOneofCase.Composite =>
                EvaluateCompositeFilter(expression.Composite, traceContext, clauseMap),
            _ => true
        };
    }

    // Error filters promote familiar TraceLens semantics (any span error marks the whole trace).
    private static bool EvaluateErrorFilter(ErrorFilter filter, TraceContext traceContext)
    {
        if (filter is null) return false;

        return filter.Mode switch
        {
            ErrorFilter.Types.Mode.OnlyErrors => traceContext.Spans.Any(SpanHasError),
            ErrorFilter.Types.Mode.OnlyNonErrors => traceContext.Spans.All(span => !SpanHasError(span)),
            _ => true
        };
    }

    // Duration filters operate on individual span runtimes to keep behaviour intuitive for trace search callers.
    private static bool EvaluateDurationFilter(DurationFilter filter, TraceContext traceContext)
    {
        if (filter is null) return false;

        var (min, max) = NormalizeDurationBounds(filter);
        if (min is null && max is null) return true;

        foreach (var span in traceContext.Spans)
        {
            var duration = GetSpanDurationNanos(span);
            if (min.HasValue && duration < min.Value) continue;

            if (max.HasValue && duration > max.Value) continue;

            return true;
        }

        return false;
    }

    private static bool EvaluateCompositeFilter(
        TraceFilterComposite composite,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (composite is null || composite.Expressions.Count == 0) return true;

        var useOr = composite.Operator == TraceFilterComposite.Types.Operator.Or;

        var result = useOr ? false : true;

        foreach (var expression in composite.Expressions)
        {
            var childResult = EvaluateTraceFilter(expression, traceContext, clauseMap);
            if (useOr)
                result |= childResult;
            else
                result &= childResult;
        }

        return result;
    }

    private static bool EvaluateServiceFilter(ServiceFilter filter, TraceContext traceContext)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Name)) return false;

        return traceContext.Spans.Any(span =>
            string.Equals(span.ServiceName, filter.Name, StringComparison.Ordinal));
    }

    private static bool EvaluateSpanNameFilter(SpanNameFilter filter, TraceContext traceContext)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Name)) return false;

        return traceContext.Spans.Any(span =>
            string.Equals(span.OperationName, filter.Name, StringComparison.Ordinal));
    }

    private static bool EvaluateAttributeFilter(
        AttributeFilter filter,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Key)) return false;

        var target = filter.Target == AttributeFilterTarget.Log
            ? AttributeFilterTarget.Log
            : AttributeFilterTarget.Span;

        var operation = NormalizeAttributeFilterOperator(filter);

        if (operation == AttributeFilterOperator.Equals && string.IsNullOrEmpty(filter.Value)) return false;

        var clauseKey = BuildClauseKey(filter.Key!, filter.Value, target, operation);

        if (!clauseMap.TryGetValue(clauseKey, out var clause))
        {
            clause = new AttributeClauseMatch
            {
                Clause = clauseKey
            };
            clauseMap[clauseKey] = clause;
        }

        var matches = target == AttributeFilterTarget.Log
            ? EvaluateLogAttributeMatches(traceContext.Logs, filter.Key!, filter.Value, operation)
            : EvaluateSpanAttributeMatches(traceContext, filter.Key!, filter.Value, operation);

        if (matches.Count > 0)
        {
            clause.Satisfied = true;
            clause.Matches.AddRange(matches);
            return true;
        }

        return false;
    }

    private static List<AttributeMatch> EvaluateSpanAttributeMatches(
        TraceContext traceContext,
        string key,
        string? value,
        AttributeFilterOperator operation)
    {
        if (traceContext.SpanAttributes is null || traceContext.SpanAttributes.Count == 0)
            return EvaluateSpanAttributeMatchesFromMap(traceContext.Spans, key, value, operation);

        var matches = new List<AttributeMatch>();

        foreach (var span in traceContext.Spans)
        {
            if (traceContext.SpanAttributes.TryGetValue(span.SpanId, out var attributes) && attributes.Count > 0)
            {
                foreach (var attribute in attributes)
                {
                    if (!string.Equals(attribute.Key, key, StringComparison.Ordinal)) continue;

                    var match = operation switch
                    {
                        AttributeFilterOperator.Equals => string.Equals(attribute.Value, value, StringComparison.Ordinal),
                        AttributeFilterOperator.NotEquals => !string.Equals(attribute.Value, value, StringComparison.Ordinal),
                        AttributeFilterOperator.Contains => attribute.Value?.Contains(value ?? string.Empty, StringComparison.Ordinal) == true,
                        AttributeFilterOperator.GreaterThan => string.Compare(attribute.Value, value, StringComparison.Ordinal) > 0,
                        AttributeFilterOperator.LessThan => string.Compare(attribute.Value, value, StringComparison.Ordinal) < 0,
                        AttributeFilterOperator.GreaterThanOrEqual => string.Compare(attribute.Value, value, StringComparison.Ordinal) >= 0,
                        AttributeFilterOperator.LessThanOrEqual => string.Compare(attribute.Value, value, StringComparison.Ordinal) <= 0,
                        _ => true // Exists or unspecified
                    };

                    if (!match) continue;

                    matches.Add(new AttributeMatch
                    {
                        SpanId = span.SpanId,
                        Key = key,
                        Value = attribute.Value ?? string.Empty
                    });
                }
            }
            else
            {
                matches.AddRange(EvaluateSpanAttributeMatchesFromMap(new[] { span }, key, value, operation));
            }
        }

        return matches;
    }

    private static List<AttributeMatch> EvaluateSpanAttributeMatchesFromMap(
        IReadOnlyList<SpanEntity> spans,
        string key,
        string? value,
        AttributeFilterOperator operation)
    {
        var matches = new List<AttributeMatch>();

        foreach (var span in spans)
        {
            if (span.AttributeMap is not { Length: > 0 }) continue;

            foreach (var attribute in span.AttributeMap)
            {
                if (!attribute.StartsWith($"{key}:", StringComparison.Ordinal)) continue;

                var matchValue = attribute.Length > key.Length + 1
                    ? attribute[(key.Length + 1)..]
                    : string.Empty;

                var match = operation switch
                {
                    AttributeFilterOperator.Equals => string.Equals(matchValue, value, StringComparison.Ordinal),
                    AttributeFilterOperator.NotEquals => !string.Equals(matchValue, value, StringComparison.Ordinal),
                    AttributeFilterOperator.Contains => matchValue.Contains(value ?? string.Empty, StringComparison.Ordinal),
                    AttributeFilterOperator.GreaterThan => string.Compare(matchValue, value, StringComparison.Ordinal) > 0,
                    AttributeFilterOperator.LessThan => string.Compare(matchValue, value, StringComparison.Ordinal) < 0,
                    AttributeFilterOperator.GreaterThanOrEqual => string.Compare(matchValue, value, StringComparison.Ordinal) >= 0,
                    AttributeFilterOperator.LessThanOrEqual => string.Compare(matchValue, value, StringComparison.Ordinal) <= 0,
                    _ => true // Exists or unspecified
                };

                if (!match) continue;

                matches.Add(new AttributeMatch
                {
                    SpanId = span.SpanId,
                    Key = key,
                    Value = matchValue
                });
            }
        }

        return matches;
    }

    private static List<AttributeMatch> EvaluateLogAttributeMatches(
        IReadOnlyList<LogEntity> logs,
        string key,
        string? value,
        AttributeFilterOperator operation)
    {
        var matches = new List<AttributeMatch>();

        foreach (var log in logs)
        {
            if (log.Attributes is not { Count: > 0 }) continue;

            foreach (var attribute in log.Attributes)
            {
                if (!string.Equals(attribute.Key, key, StringComparison.Ordinal)) continue;

                var match = operation switch
                {
                    AttributeFilterOperator.Equals => string.Equals(attribute.Value, value, StringComparison.Ordinal),
                    AttributeFilterOperator.NotEquals => !string.Equals(attribute.Value, value, StringComparison.Ordinal),
                    AttributeFilterOperator.Contains => attribute.Value?.Contains(value ?? string.Empty, StringComparison.Ordinal) == true,
                    AttributeFilterOperator.GreaterThan => string.Compare(attribute.Value, value, StringComparison.Ordinal) > 0,
                    AttributeFilterOperator.LessThan => string.Compare(attribute.Value, value, StringComparison.Ordinal) < 0,
                    AttributeFilterOperator.GreaterThanOrEqual => string.Compare(attribute.Value, value, StringComparison.Ordinal) >= 0,
                    AttributeFilterOperator.LessThanOrEqual => string.Compare(attribute.Value, value, StringComparison.Ordinal) <= 0,
                    _ => true // Exists or unspecified
                };

                if (!match) continue;

                matches.Add(new AttributeMatch
                {
                    SpanId = log.SpanId,
                    Key = key,
                    Value = attribute.Value ?? string.Empty
                });
            }
        }

        return matches;
    }

    private static string BuildClauseKey(
        string key,
        string? value,
        AttributeFilterTarget target,
        AttributeFilterOperator operation)
    {
        var prefix = target == AttributeFilterTarget.Log ? "log" : "tag";

        if (operation == AttributeFilterOperator.Equals && !string.IsNullOrEmpty(value))
            return $"{prefix}:{key}={value}";

        return $"{prefix}:{key}";
    }

    private static (ulong? Min, ulong? Max) NormalizeDurationBounds(DurationFilter? filter)
    {
        if (filter is null) return (null, null);

        var min = filter.MinNanos > 0 ? (ulong?)filter.MinNanos : null;
        var max = filter.MaxNanos > 0 ? (ulong?)filter.MaxNanos : null;
        return (min, max);
    }

    private static ulong GetSpanDurationNanos(SpanEntity span)
    {
        var duration = span.EndTimestamp - span.StartTimestamp;
        return duration <= 0 ? 0UL : (ulong)duration;
    }

    private readonly record struct TraceContext(
        IReadOnlyList<SpanEntity> Spans,
        IReadOnlyList<LogEntity> Logs,
        IReadOnlyDictionary<string, IReadOnlyList<SpanAttributeValueEntity>>? SpanAttributes);

    private static bool SpanHasError(SpanEntity span)
    {
        if (span.AttributeMap is not { Length: > 0 }) return false;

        return span.AttributeMap.Any(attribute =>
            string.Equals(attribute, "status.code:STATUS_CODE_ERROR", StringComparison.Ordinal) ||
            attribute.Contains("error", StringComparison.OrdinalIgnoreCase));
    }
}