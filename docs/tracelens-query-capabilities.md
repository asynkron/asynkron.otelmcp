# TraceLens Query Capabilities and Performance

This document describes the complete query capabilities of the SearchTraces API, including supported
filter combinations, performance characteristics, and database indexes.

## Supported Filter Types

### 1. Service Filters
- **Exact match**: Filter traces by service name
- **Performance**: Uses indexed lookup on `Spans.ServiceName`
- **Example**: `service.name = "checkout-service"`

### 2. Span Name Filters
- **Exact match**: Filter traces by operation/span name
- **Performance**: Uses indexed lookup on `Spans.OperationName`
- **Example**: `span.name = "HTTP GET /api/users"`

### 3. Attribute Filters

#### Supported Operators
1. **EQUALS** (`=`): Exact value match
   - Example: `http.method = "GET"`
   
2. **EXISTS**: Check if attribute key exists (value ignored)
   - Example: `error EXISTS`
   
3. **CONTAINS**: Substring match
   - Example: `http.url CONTAINS "/api/v1/"`
   
4. **NOT_EQUALS** (`!=`): Inverse equality
   - Example: `deployment.environment != "test"`
   
5. **GREATER_THAN** (`>`): Lexicographic comparison
   - Example: `http.status_code > "399"` (matches 4xx, 5xx)
   
6. **LESS_THAN** (`<`): Lexicographic comparison
   - Example: `http.status_code < "300"` (matches 1xx, 2xx)
   
7. **GREATER_THAN_OR_EQUAL** (`>=`): Lexicographic comparison
   - Example: `http.status_code >= "400"`
   
8. **LESS_THAN_OR_EQUAL** (`<=`): Lexicographic comparison
   - Example: `http.status_code <= "299"`

#### Attribute Targets
- **SPAN** (default): Search span attributes
- **LOG**: Search log attributes (both resource and record attributes)

#### Performance
- All attribute filters are **pushed to SQL** for efficient database-level filtering
- Uses composite indexes: `(SpanId, Key, Value)` for span attributes, `(LogId, Key, Value)` for log attributes
- Attribute-only queries (no service/time filter) will scan the attribute tables but use indexes

### 4. Error Filters
- **MODE_ONLY_ERRORS**: Return only traces with at least one error span
- **MODE_ONLY_NON_ERRORS**: Return only traces with no error spans
- **Detection**: Checks for `status.code = STATUS_CODE_ERROR` or attributes containing "error"
- **Performance**: Evaluated in-memory after SQL filtering

### 5. Duration Filters
- **MinNanos**: Minimum span duration (inclusive)
- **MaxNanos**: Maximum span duration (inclusive)
- **Behavior**: Matches traces with at least one span in the duration range
- **Performance**: Pushed to SQL using computed column `(EndTimestamp - StartTimestamp)`

### 6. Time Range Filters
- **StartTime**: Filter by span start time (Unix nanoseconds)
- **EndTime**: Filter by span end time (Unix nanoseconds)
- **Performance**: Uses indexed lookup on `Spans.StartTimestamp` and `Spans.EndTimestamp`

### 7. Composite Filters
- **AND**: All child expressions must match
- **OR**: At least one child expression must match
- **Nesting**: Unlimited nesting depth supported
- **Performance**: AND filters with required attributes are pushed to SQL; OR filters may require in-memory evaluation

## Database Indexes

### Spans Table
```sql
-- Single-column indexes
Spans(SpanId) PRIMARY KEY
Spans(TraceId)
Spans(ServiceName)
Spans(OperationName)
Spans(StartTimestamp)
Spans(EndTimestamp)

-- Composite indexes for common query patterns
Spans(ServiceName, StartTimestamp)  -- Service + time range queries
Spans(ServiceName, EndTimestamp)    -- Service + end time queries
Spans(TraceId, StartTimestamp)      -- Trace timeline queries
```

### SpanAttributeValues Table
```sql
-- Single-column indexes
SpanAttributeValues(SpanId)
SpanAttributeValues(Key)
SpanAttributeValues(Value)
SpanAttributeValues(Key, Value)

-- Composite covering index
SpanAttributeValues(SpanId, Key, Value)  -- Optimizes attribute filters
```

### LogAttributes Table
```sql
-- Single-column indexes
LogAttributes(LogId)
LogAttributes(Key)
LogAttributes(Value)
LogAttributes(Key, Value)

-- Composite covering index
LogAttributes(LogId, Key, Value)  -- Optimizes log attribute filters
```

## Query Performance Characteristics

### Fast Queries (Fully Indexed)
✅ Service + time range
✅ Service + attribute (exact match)
✅ Service + span name
✅ Service + duration range
✅ Attribute key-value lookup (any target)
✅ Single service filter

### Moderate Performance
⚠️ Attribute-only queries (no service filter) - scans attribute tables with indexes
⚠️ Complex OR filters with multiple attribute conditions - may require in-memory evaluation
⚠️ Duration filters without service/time filter - scans full span table

### Considerations
- **OR filters with attributes**: Currently evaluated in-memory after SQL pre-filtering. 
  For best performance, combine with service or time filters to reduce result set size.
- **String comparisons**: Lexicographic (alphabetical) order, not numeric. 
  Use zero-padding for numeric comparisons (e.g., "0200", "0404", "0500").
- **Attribute existence**: Very fast with `EXISTS` operator, uses key-only index.

## Unsupported Query Combinations

The following are **NOT** currently supported:

❌ **NOT operator**: No way to negate a composite filter
❌ **IN operator**: No multi-value attribute matching (use OR with multiple EQUALS)
❌ **REGEX operator**: No regular expression matching
❌ **Numeric comparisons**: String comparison only (values stored as strings)
❌ **Case-insensitive matching**: All comparisons are case-sensitive
❌ **Wildcard matching**: No `*` or `%` wildcards (use CONTAINS for substring)

## Query Optimization Tips

1. **Always include service filters** when possible - this uses the most selective index
2. **Combine service + time range** for fastest queries using composite index
3. **Use EQUALS for exact matches** - faster than CONTAINS
4. **Avoid OR with many attribute conditions** - consider multiple queries instead
5. **Use EXISTS instead of checking for empty values** - optimized key-only lookup
6. **Pad numeric values** for proper comparison (e.g., "0200", "0404")

## Example Optimal Query Patterns

### Pattern 1: Service + Time + Attributes
```json
{
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "service": { "name": "api-service" } },
        { "attribute": { "key": "http.status_code", "value": "400", "operator": "GREATER_THAN_OR_EQUAL" } }
      ]
    }
  },
  "start_time": 1234567890000000000,
  "end_time": 1234567899999999999
}
```
**Performance**: Excellent - uses `(ServiceName, StartTimestamp)` index + attribute filter

### Pattern 2: Error Traces in Time Range
```json
{
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "service": { "name": "payment-service" } },
        { "error": { "mode": "MODE_ONLY_ERRORS" } }
      ]
    }
  },
  "start_time": 1234567890000000000,
  "end_time": 1234567899999999999
}
```
**Performance**: Good - service+time indexed, error check in-memory

### Pattern 3: Complex Attribute Combinations
```json
{
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
**Performance**: Good - service filter narrows results, OR evaluation efficient

## Migration Path

To use the new query capabilities on existing databases:
1. Apply migration `20251103172313_OptimizeSearchIndexes` to add composite indexes
2. No data migration required
3. Queries are backward compatible
4. New operators available immediately after migration
