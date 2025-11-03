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
  - a leaf predicate (`ServiceFilter`, `SpanNameFilter`, `AttributeFilter`, `ErrorFilter`, or `DurationFilter`).
- `LogFilter` retains the previous substring search for log bodies associated with the returned traces.

Attribute predicates support multiple operators:
- `ATTRIBUTE_FILTER_OPERATOR_EQUALS` (default) - exact match
- `ATTRIBUTE_FILTER_OPERATOR_EXISTS` - checks if the key exists (value is ignored)
- `ATTRIBUTE_FILTER_OPERATOR_CONTAINS` - substring match
- `ATTRIBUTE_FILTER_OPERATOR_NOT_EQUALS` - inverse equality check
- `ATTRIBUTE_FILTER_OPERATOR_GREATER_THAN` - lexicographic comparison
- `ATTRIBUTE_FILTER_OPERATOR_LESS_THAN` - lexicographic comparison
- `ATTRIBUTE_FILTER_OPERATOR_GREATER_THAN_OR_EQUAL` - lexicographic comparison
- `ATTRIBUTE_FILTER_OPERATOR_LESS_THAN_OR_EQUAL` - lexicographic comparison

When no operator is specified, the filter defaults to equality when a value is supplied and
falls back to an "exists" check when only a key is provided. Set `target` to 
`ATTRIBUTE_FILTER_TARGET_SPAN` (the default) to search span attributes or 
`ATTRIBUTE_FILTER_TARGET_LOG` to search log attributes.

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

## Advanced Operator Examples

Search for traces with HTTP status codes indicating errors (>= 400):

```json
{
  "limit": 20,
  "filter": {
    "attribute": {
      "key": "http.status_code",
      "value": "400",
      "operator": "ATTRIBUTE_FILTER_OPERATOR_GREATER_THAN_OR_EQUAL"
    }
  }
}
```

Search for traces where the URL contains a specific path:

```json
{
  "limit": 20,
  "filter": {
    "attribute": {
      "key": "http.url",
      "value": "/api/v1/",
      "operator": "ATTRIBUTE_FILTER_OPERATOR_CONTAINS"
    }
  }
}
```

Search for traces excluding a specific service:

```json
{
  "limit": 20,
  "filter": {
    "attribute": {
      "key": "deployment.environment",
      "value": "test",
      "operator": "ATTRIBUTE_FILTER_OPERATOR_NOT_EQUALS"
    }
  }
}
```

Combine error filtering with duration bounds:

```json
{
  "limit": 20,
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "error": { "mode": "MODE_ONLY_ERRORS" } },
        { "duration": { "min_nanos": 1000000000 } }
      ]
    }
  }
}
```
