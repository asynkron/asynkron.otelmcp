# TraceLens Search Filters

The `SearchTraces` gRPC/MCP command accepts a composable filter expression so callers can
combine service, span name, and attribute predicates without chaining multiple requests.

## Request shape

```proto
message SearchTracesRequest {
  TraceFilterExpression filter = 9;
  uint64 start_time = 5;
  uint64 end_time = 6;
  int32 limit = 7;
  LogFilter log_filter = 10;
}
```

- `filter` is a tree of `TraceFilterExpression` nodes. Each node is either:
  - a composite (`TraceFilterComposite`) combining child expressions with `OPERATOR_AND` or `OPERATOR_OR`;
  - a leaf predicate (`ServiceFilter`, `SpanNameFilter`, `AttributeFilter`, `ErrorFilter`, `DurationFilter`, `SpanKindFilter`, `TraceDurationFilter`).
- `LogFilter` retains the previous substring search for log bodies associated with the returned traces.

Attribute predicates default to equality when a value is supplied and fall back to an
"exists" check when only a key is provided. Set `target` to `ATTRIBUTE_FILTER_TARGET_SPAN`
(the default) to search span attributes, `ATTRIBUTE_FILTER_TARGET_RESOURCE` for resource
attributes, or `ATTRIBUTE_FILTER_TARGET_LOG` for log attributes.

Error predicates filter traces containing spans with error status (`MODE_ONLY_ERRORS`) or
without errors (`MODE_ONLY_NON_ERRORS`).

Duration predicates filter by individual span duration (min/max nanoseconds).

Span kind predicates filter traces containing spans of a specific kind: `SPAN_KIND_CLIENT`,
`SPAN_KIND_SERVER`, `SPAN_KIND_INTERNAL`, `SPAN_KIND_PRODUCER`, or `SPAN_KIND_CONSUMER`.

Trace duration predicates filter by total trace duration (earliest span start to latest
span end), distinct from individual span duration filtering.

## Examples

Search for traces from `checkout-service` where any span has either
`http.method=GET` *or* `http.method=POST`:

```json
{
  "limit": 20,
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "service": { "name": "checkout-service" } },
        {
          "composite": {
            "operator": "OPERATOR_OR",
            "expressions": [
              { "attribute": { "key": "http.method", "value": "GET" } },
              { "attribute": { "key": "http.method", "value": "POST" } }
            ]
          }
        }
      ]
    }
  }
}
```

Add an additional `attribute` child under an `OPERATOR_AND` node to require multiple
key/value pairs on the same span. Combine composites for richer boolean logic without
introducing custom SQL per client.

### Example: Filter by Span Kind

Search for traces containing CLIENT spans:

```json
{
  "limit": 20,
  "filter": {
    "span_kind": {
      "kind": "SPAN_KIND_CLIENT"
    }
  }
}
```

### Example: Filter by Resource Attribute

Search for traces from production environment:

```json
{
  "limit": 20,
  "filter": {
    "attribute": {
      "key": "deployment.environment",
      "value": "production",
      "target": "ATTRIBUTE_FILTER_TARGET_RESOURCE"
    }
  }
}
```

### Example: Filter by Trace Duration

Search for slow traces (over 5 seconds end-to-end):

```json
{
  "limit": 20,
  "filter": {
    "trace_duration": {
      "min_nanos": 5000000000
    }
  }
}
```

### Example: Composite Filter with Span Kind and Duration

Search for slow CLIENT calls:

```json
{
  "limit": 20,
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "span_kind": { "kind": "SPAN_KIND_CLIENT" } },
        { "duration": { "min_nanos": 100000000 } }
      ]
    }
  }
}
```
