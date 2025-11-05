using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

/// <summary>
/// Comprehensive tests for the MCP (Model Context Protocol) streaming endpoint.
/// Tests verify that all MCP commands work correctly and that the server properly
/// connects and interacts with the MCP API.
/// </summary>
[Collection("GrpcIntegration")]
public class McpComprehensiveTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions CommandSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly OtelReceiverApplicationFactory _factory;

    static McpComprehensiveTests()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public McpComprehensiveTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task McpEndpoint_SendsHandshakeOnConnection()
    {
        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getMetricNames", new { })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        var handshake = await ReadEnvelopeAsync(reader);
        
        Assert.Equal("ready", handshake.GetProperty("type").GetString());
        Assert.Equal("mcp", handshake.GetProperty("protocol").GetString());
        Assert.Equal("1.0", handshake.GetProperty("version").GetString());
        
        var commands_prop = handshake.GetProperty("commands");
        Assert.True(commands_prop.GetArrayLength() > 0);
        
        var commandsList = commands_prop.EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains("getSearchData", commandsList);
        Assert.Contains("getValuesForTag", commandsList);
        Assert.Contains("searchTraces", commandsList);
        Assert.Contains("getMetricNames", commandsList);
        Assert.Contains("getMetric", commandsList);
    }

    [Fact]
    public async Task McpCommand_GetSearchData_ReturnsSearchMetadata()
    {
        // Ingest test data first
        await IngestTestTraceAsync("mcp-search-service", "mcp-search-op");

        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getSearchData", new { })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("result", result.GetProperty("type").GetString());
        Assert.Equal("getSearchData", result.GetProperty("command").GetString());
        
        var resultContent = result.GetProperty("result").GetRawText();
        Assert.Contains("mcp-search-service", resultContent);
    }

    [Fact]
    public async Task McpCommand_GetValuesForTag_ReturnsTagValues()
    {
        // Ingest test data with attributes
        await IngestTestTraceWithAttributesAsync("mcp-tag-service", "mcp-tag-op",
            new[] { ("http.method", "POST"), ("environment", "test") });

        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getValuesForTag", new { tagName = "http.method" })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("result", result.GetProperty("type").GetString());
        Assert.Equal("getValuesForTag", result.GetProperty("command").GetString());
    }

    [Fact]
    public async Task McpCommand_SearchTraces_FindsMatchingTraces()
    {
        var traceId = await IngestTestTraceAsync("mcp-search-traces-service", "mcp-search-traces-op");

        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "searchTraces", new
            {
                limit = 10,
                filter = new
                {
                    service = new { name = "mcp-search-traces-service" }
                }
            })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("result", result.GetProperty("type").GetString());
        Assert.Equal("searchTraces", result.GetProperty("command").GetString());
        
        var resultContent = result.GetProperty("result").GetRawText();
        Assert.Contains(traceId, resultContent);
        Assert.Contains("mcp-search-traces-op", resultContent);
    }

    [Fact]
    public async Task McpCommand_GetMetricNames_ReturnsMetricList()
    {
        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getMetricNames", new { })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("result", result.GetProperty("type").GetString());
        Assert.Equal("getMetricNames", result.GetProperty("command").GetString());
    }

    [Fact]
    public async Task McpCommand_GetMetric_HandlesRequest()
    {
        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getMetric", new { name = "test_metric" })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("result", result.GetProperty("type").GetString());
        Assert.Equal("getMetric", result.GetProperty("command").GetString());
    }

    [Fact]
    public async Task McpEndpoint_HandlesMultipleCommandsSequentially()
    {
        await IngestTestTraceAsync("multi-cmd-service", "multi-cmd-op");

        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "getMetricNames", new { }),
            SerializeCommand("2", "getSearchData", new { }),
            SerializeCommand("3", "searchTraces", new { limit = 5 })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        var handshake = await ReadEnvelopeAsync(reader);
        Assert.Equal("ready", handshake.GetProperty("type").GetString());

        // Read three responses
        for (int i = 1; i <= 3; i++)
        {
            var result = await ReadEnvelopeAsync(reader);
            Assert.Equal("result", result.GetProperty("type").GetString());
            Assert.Equal(i.ToString(), result.GetProperty("id").GetString());
        }
    }

    [Fact]
    public async Task McpEndpoint_HandlesInvalidCommandGracefully()
    {
        var httpClient = _factory.CreateDefaultClient();
        httpClient.Timeout = DefaultTimeout;

        var commands = new[]
        {
            SerializeCommand("1", "invalidCommand", new { })
        };

        var requestBody = string.Join("\n", commands);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        // Read handshake
        await ReadEnvelopeAsync(reader);

        // Read error response
        var result = await ReadEnvelopeAsync(reader);
        Assert.Equal("error", result.GetProperty("type").GetString());
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.NotNull(error.GetProperty("message").GetString());
    }

    // Helper methods

    private static string SerializeCommand(string id, string command, object payload)
    {
        return JsonSerializer.Serialize(new
        {
            id,
            command,
            payload
        }, CommandSerializerOptions);
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(StreamReader reader)
    {
        var line = await reader.ReadLineAsync();
        while (line is not null)
        {
            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6);
                return JsonDocument.Parse(json).RootElement;
            }
            line = await reader.ReadLineAsync();
        }
        throw new InvalidOperationException("No SSE data line found.");
    }

    private async Task<string> IngestTestTraceAsync(string serviceName, string operationName)
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new TraceService.TraceServiceClient(channel);

        var traceIdBytes = Guid.NewGuid().ToByteArray();
        var spanIdBytes = Guid.NewGuid().ToByteArray().Take(8).ToArray();
        var traceIdHex = Convert.ToHexString(traceIdBytes);
        var spanIdHex = Convert.ToHexString(spanIdBytes);

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = serviceName }
                            }
                        }
                    },
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Name = operationName,
                                    StartTimeUnixNano = 1000,
                                    EndTimeUnixNano = 2000
                                }
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request);

        // Wait for the trace to be persisted (with retries)
        for (int i = 0; i < 50; i++)
        {
            var exists = await _factory.ExecuteDbContextAsync(async context =>
                await context.Spans.AnyAsync(s => s.SpanId == spanIdHex));
            if (exists) break;
            await Task.Delay(100);
        }

        return traceIdHex;
    }

    private async Task IngestTestTraceWithAttributesAsync(string serviceName, string operationName,
        (string key, string value)[] attributes)
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new TraceService.TraceServiceClient(channel);

        var traceIdBytes = Guid.NewGuid().ToByteArray();
        var spanIdBytes = Guid.NewGuid().ToByteArray().Take(8).ToArray();
        var spanIdHex = Convert.ToHexString(spanIdBytes);

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(traceIdBytes),
            SpanId = ByteString.CopyFrom(spanIdBytes),
            Name = operationName,
            StartTimeUnixNano = 1000,
            EndTimeUnixNano = 2000
        };

        foreach (var (key, value) in attributes)
        {
            span.Attributes.Add(new KeyValue
            {
                Key = key,
                Value = new AnyValue { StringValue = value }
            });
        }

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = serviceName }
                            }
                        }
                    },
                    ScopeSpans =
                    {
                        new ScopeSpans { Spans = { span } }
                    }
                }
            }
        };

        await client.ExportAsync(request);
        
        // Wait for the trace to be persisted (with retries)
        for (int i = 0; i < 50; i++)
        {
            var exists = await _factory.ExecuteDbContextAsync(async context =>
                await context.Spans.AnyAsync(s => s.SpanId == spanIdHex));
            if (exists) break;
            await Task.Delay(100);
        }
    }
}
