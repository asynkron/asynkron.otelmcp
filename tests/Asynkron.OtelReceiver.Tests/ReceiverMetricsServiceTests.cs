using Asynkron.OtelReceiver.Monitoring.V1;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[Collection("GrpcIntegration")]
public class ReceiverMetricsServiceTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly OtelReceiverApplicationFactory _factory;

    static ReceiverMetricsServiceTests()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public ReceiverMetricsServiceTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubscribeMetrics_StreamsLiveMetricsUpdates()
    {
        using var channel = _factory.CreateGrpcChannel();
        var metricsClient = new ReceiverMetricsService.ReceiverMetricsServiceClient(channel);
        var traceClient = new TraceService.TraceServiceClient(channel);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var call = metricsClient.SubscribeMetrics(new Empty(), cancellationToken: cts.Token);

        var updates = new List<ReceiverMetricsUpdate>();
        var readTask = Task.Run(async () =>
        {
            while (await call.ResponseStream.MoveNext(cts.Token))
            {
                updates.Add(call.ResponseStream.Current);
                if (updates.Count >= 2) break;
            }
        }, cts.Token);

        // Give the stream time to start
        await Task.Delay(100);

        // Send a trace to trigger metrics updates
        var traceIdBytes = Enumerable.Range(100, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(200, 8).Select(i => (byte)i).ToArray();

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
                                Value = new AnyValue { StringValue = "metrics-test-service" }
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
                                    Name = "metrics-test-span",
                                    StartTimeUnixNano = 1000,
                                    EndTimeUnixNano = 2000
                                }
                            }
                        }
                    }
                }
            }
        };

        await traceClient.ExportAsync(request);

        await readTask;

        Assert.NotEmpty(updates);
        Assert.True(updates.Any(u => u.SpansReceived > 0),
            "Expected at least one update with spans received");
    }

    [Fact]
    public async Task SubscribeMetrics_ConnectsSuccessfully()
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new ReceiverMetricsService.ReceiverMetricsServiceClient(channel);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var call = client.SubscribeMetrics(new Empty(), cancellationToken: cts.Token);

        var receivedAnyUpdate = await call.ResponseStream.MoveNext(cts.Token);
        if (receivedAnyUpdate)
        {
            var update = call.ResponseStream.Current;
            Assert.NotNull(update);
        }

        Assert.True(receivedAnyUpdate, "Should receive at least one metrics update");
    }
}
