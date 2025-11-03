using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Asynkron.OtelReceiver.Data;
using Google.Protobuf;
using OtelMcp.Proto.V1;

namespace Asynkron.OtelReceiver.Services;

/// <summary>
/// Minimal Model Context Protocol (MCP) handler that mirrors the TraceLens data gRPC surface
/// over a newline-delimited JSON (NDJSON) streaming HTTP endpoint.
/// </summary>
public static class McpStreamingEndpoint
{
    private static readonly JsonParser Parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default
        .WithFormatDefaultValues(false)
        .WithPreserveProtoFieldNames(false));

    private static readonly Dictionary<string, ComponentMetadata> ComponentMetadataStore = new();

    private static readonly Dictionary<string, Func<ModelRepo, JsonElement, Task<IMessage>>> CommandHandlers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["getSearchData"] = async (repo, payload) =>
                await repo.GetSearchData(Parse<GetSearchDataRequest>(payload)),
            ["getValuesForTag"] = async (repo, payload) =>
                await repo.GetValuesForTag(Parse<GetValuesForTagRequest>(payload)),
            ["searchTraces"] = async (repo, payload) =>
                await repo.SearchTraces(Parse<SearchTracesRequest>(payload)),
            ["getMetricNames"] = async (repo, _) =>
                await repo.GetMetricNames(),
            ["getMetric"] = async (repo, payload) => await repo.GetMetric(Parse<GetMetricRequest>(payload))
        };

    public static RouteHandlerBuilder MapMcpStreamingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/mcp/stream", HandleAsync)
            .WithName("McpStreamingEndpoint");
    }

    private static async Task HandleAsync(HttpContext context, ModelRepo repo, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("McpStreaming");

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/x-ndjson";

        await WriteHandshakeAsync(context.Response, logger, cancellationToken);

        await foreach (var command in ReadCommandsAsync(context.Request.Body, cancellationToken))
        {
            var payloadForLog = FormatPayloadForLog(command.Payload);
            logger.LogInformation("Received MCP command {Command} (Id: {Id}) with payload: {Payload}", command.Command,
                command.Id, payloadForLog);
            McpResponse envelope;
            try
            {
                if (string.Equals(command.Command, "setComponentMetadata", StringComparison.OrdinalIgnoreCase))
                {
                    envelope = HandleSetComponentMetadata(command);
                }
                else if (string.Equals(command.Command, "getMetadataForComponent", StringComparison.OrdinalIgnoreCase))
                {
                    envelope = HandleGetMetadataForComponent(command);
                }
                else
                {
                    var result = await ExecuteCommandAsync(command, repo);
                    envelope = McpResponse.ForResult(command.Id, command.Command, result);
                }
                
                var responsePayload = envelope.Result is JsonElement resultElement
                    ? resultElement.GetRawText()
                    : "{}";
                logger.LogInformation("MCP command {Command} (Id: {Id}) succeeded with response: {Response}",
                    command.Command, command.Id, responsePayload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute MCP command {Command}", command.Command);
                envelope = McpResponse.ForError(command.Id, command.Command, ex.Message);
            }

            await WriteEnvelopeAsync(context.Response, envelope, cancellationToken);
            logger.LogInformation("Sent MCP response for {Command} (Id: {Id})", command.Command, command.Id);
        }
    }

    private static async Task<IMessage> ExecuteCommandAsync(McpCommand command, ModelRepo repo)
    {
        if (!CommandHandlers.TryGetValue(command.Command, out var handler))
            throw new InvalidOperationException($"Unknown MCP command '{command.Command}'.");

        return await handler(repo, command.Payload);
    }

    private static async Task WriteHandshakeAsync(HttpResponse response, ILogger logger,
        CancellationToken cancellationToken)
    {
        var handshake = new McpHandshake
        {
            Type = "ready",
            Protocol = "mcp",
            Version = "1.0",
            Capabilities = new[] { "commands" },
            Commands = CommandHandlers.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray()
        };

        await JsonSerializer.SerializeAsync(response.Body, handshake, SerializerOptions, cancellationToken);
        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
        logger.LogInformation("Sent MCP handshake advertising commands: {Commands}",
            string.Join(", ", handshake.Commands));
    }

    private static async Task WriteEnvelopeAsync(HttpResponse response, McpResponse envelope,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(response.Body, envelope, SerializerOptions, cancellationToken);
        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async IAsyncEnumerable<McpCommand> ReadCommandsAsync(Stream requestBody,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(requestBody, Encoding.UTF8, false, leaveOpen: true);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (line is null) yield break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            McpCommand command;
            try
            {
                command = JsonSerializer.Deserialize<McpCommand>(line, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Invalid MCP command payload.", ex);
            }

            if (string.IsNullOrWhiteSpace(command.Command))
                throw new InvalidOperationException("MCP command must include a command name.");

            yield return command;
        }
    }

    private static string FormatPayloadForLog(JsonElement payload)
    {
        return payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? "<empty>"
            : payload.GetRawText();
    }

    private static T Parse<T>(JsonElement payload)
        where T : IMessage<T>, new()
    {
        if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return new T();

        var json = payload.GetRawText();
        return Parser.Parse<T>(json);
    }

    private readonly record struct McpCommand(string? Id, string Command, JsonElement Payload);

    private sealed record McpHandshake
    {
        public string Type { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string[] Capabilities { get; init; } = Array.Empty<string>();
        public string[] Commands { get; init; } = Array.Empty<string>();
    }

    private sealed record McpResponse
    {
        public string Type { get; init; } = "result";
        public string Command { get; init; } = string.Empty;
        public string? Id { get; init; }
        public JsonElement? Result { get; init; }
        public McpError? Error { get; init; }

        public static McpResponse ForResult(string? id, string command, IMessage result)
        {
            var json = Formatter.Format(result);
            using var document = JsonDocument.Parse(json);
            var normalized = NormalizeToCamelCase(document.RootElement);
            return new McpResponse
            {
                Type = "result",
                Command = command,
                Id = id,
                Result = normalized
            };
        }

        public static McpResponse ForError(string? id, string command, string message)
        {
            return new McpResponse
            {
                Type = "error",
                Command = command,
                Id = id,
                Error = new McpError
                {
                    Code = "command_failed",
                    Message = message
                }
            };
        }
    }

    private sealed record McpError
    {
        public string Code { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    private static JsonElement NormalizeToCamelCase(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCamelCase(writer, element);
        }

        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        using var normalized = JsonDocument.Parse(sequence);
        return normalized.RootElement.Clone();
    }

    private static void WriteCamelCase(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var propertyName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                    writer.WritePropertyName(propertyName);
                    WriteCamelCase(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCamelCase(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static McpResponse HandleSetComponentMetadata(McpCommand command)
    {
        var namePath = command.Payload.GetProperty("namePath").GetString() ?? string.Empty;
        var annotations = command.Payload.GetProperty("annotations").GetString() ?? string.Empty;

        var (groupName, componentName) = ParseComponentId(namePath);
        
        ComponentMetadataStore[namePath] = new ComponentMetadata
        {
            NamePath = namePath,
            GroupName = groupName,
            ComponentName = componentName,
            ComponentKind = "Service",
            Annotation = annotations
        };

        var response = new { success = true };
        var json = JsonSerializer.Serialize(response, SerializerOptions);
        using var document = JsonDocument.Parse(json);
        
        return new McpResponse
        {
            Type = "result",
            Command = command.Command,
            Id = command.Id,
            Result = document.RootElement.Clone()
        };
    }

    private static McpResponse HandleGetMetadataForComponent(McpCommand command)
    {
        var componentId = command.Payload.GetProperty("componentId").GetString() ?? string.Empty;

        if (ComponentMetadataStore.TryGetValue(componentId, out var metadata))
        {
            var response = new
            {
                groupName = metadata.GroupName,
                componentName = metadata.ComponentName,
                componentKind = metadata.ComponentKind,
                annotation = metadata.Annotation
            };
            
            var json = JsonSerializer.Serialize(response, SerializerOptions);
            using var document = JsonDocument.Parse(json);
            
            return new McpResponse
            {
                Type = "result",
                Command = command.Command,
                Id = command.Id,
                Result = document.RootElement.Clone()
            };
        }

        var (groupName, componentName) = ParseComponentId(componentId);
        var defaultResponse = new
        {
            groupName,
            componentName,
            componentKind = "Service",
            annotation = string.Empty
        };
        
        var defaultJson = JsonSerializer.Serialize(defaultResponse, SerializerOptions);
        using var defaultDocument = JsonDocument.Parse(defaultJson);
        
        return new McpResponse
        {
            Type = "result",
            Command = command.Command,
            Id = command.Id,
            Result = defaultDocument.RootElement.Clone()
        };
    }

    private static (string GroupName, string ComponentName) ParseComponentId(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId)) return (string.Empty, string.Empty);

        var parts = componentId.Split(':', 2, StringSplitOptions.TrimEntries);

        return parts.Length == 2
            ? (parts[0], parts[1])
            : (componentId, componentId);
    }

    private sealed record ComponentMetadata
    {
        public string NamePath { get; init; } = string.Empty;
        public string GroupName { get; init; } = string.Empty;
        public string ComponentName { get; init; } = string.Empty;
        public string ComponentKind { get; init; } = string.Empty;
        public string Annotation { get; init; } = string.Empty;
    }
}