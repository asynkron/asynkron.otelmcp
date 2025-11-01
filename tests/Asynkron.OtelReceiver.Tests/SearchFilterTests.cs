using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Tracelens.Proto.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[Collection("GrpcIntegration")]
public class SearchFilterTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly OtelReceiverApplicationFactory _factory;

    static SearchFilterTests()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public SearchFilterTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchTraces_SpanKindFilter_ReturnsMatchingTraces()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        // Create trace with CLIENT span
        var clientTraceId = Enumerable.Range(100, 16).Select(i => (byte)i).ToArray();
        var clientSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 100)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "client-service" } }
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
                                    TraceId = ByteString.CopyFrom(clientTraceId),
                                    SpanId = ByteString.CopyFrom(clientSpanId),
                                    Name = "http-get",
                                    Kind = Span.Types.SpanKind.Client,
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 2_000_000
                                }
                            }
                        }
                    }
                }
            }
        });

        // Create trace with SERVER span
        var serverTraceId = Enumerable.Range(200, 16).Select(i => (byte)i).ToArray();
        var serverSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 200)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "server-service" } }
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
                                    TraceId = ByteString.CopyFrom(serverTraceId),
                                    SpanId = ByteString.CopyFrom(serverSpanId),
                                    Name = "handle-request",
                                    Kind = Span.Types.SpanKind.Server,
                                    StartTimeUnixNano = 1_500_000,
                                    EndTimeUnixNano = 2_500_000
                                }
                            }
                        }
                    }
                }
            }
        });

        var clientTraceIdHex = Convert.ToHexString(clientTraceId);
        var serverTraceIdHex = Convert.ToHexString(serverTraceId);
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == clientTraceIdHex)),
            "client trace to be queryable");
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == serverTraceIdHex)),
            "server trace to be queryable");

        // Search for CLIENT spans only from client-service
        var clientSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            SpanKind = new SpanKindFilter
                            {
                                Kind = SpanKindFilter.Types.SpanKind.Client
                            }
                        },
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "client-service"
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(clientSearch.Results);
        Assert.All(clientSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "client-service"));

        // Search for SERVER spans only from server-service
        var serverSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            SpanKind = new SpanKindFilter
                            {
                                Kind = SpanKindFilter.Types.SpanKind.Server
                            }
                        },
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "server-service"
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(serverSearch.Results);
        Assert.All(serverSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "server-service"));
    }

    [Fact]
    public async Task SearchTraces_TraceDurationFilter_ReturnsMatchingTraces()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        // Create fast trace (1ms)
        var fastTraceId = Enumerable.Range(10, 16).Select(i => (byte)i).ToArray();
        var fastSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 10)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "fast-service" } }
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
                                    TraceId = ByteString.CopyFrom(fastTraceId),
                                    SpanId = ByteString.CopyFrom(fastSpanId),
                                    Name = "fast-op",
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 2_000_000 // 1ms
                                }
                            }
                        }
                    }
                }
            }
        });

        // Create slow trace (100ms)
        var slowTraceId = Enumerable.Range(20, 16).Select(i => (byte)i).ToArray();
        var slowSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 20)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "slow-service" } }
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
                                    TraceId = ByteString.CopyFrom(slowTraceId),
                                    SpanId = ByteString.CopyFrom(slowSpanId),
                                    Name = "slow-op",
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 101_000_000 // 100ms
                                }
                            }
                        }
                    }
                }
            }
        });

        var fastTraceIdHex = Convert.ToHexString(fastTraceId);
        var slowTraceIdHex = Convert.ToHexString(slowTraceId);
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == fastTraceIdHex)),
            "fast trace to be queryable");
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == slowTraceIdHex)),
            "slow trace to be queryable");

        // Search for traces longer than 50ms from slow-service
        var slowSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            TraceDuration = new TraceDurationFilter
                            {
                                MinNanos = 50_000_000 // 50ms
                            }
                        },
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "slow-service"
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(slowSearch.Results);
        Assert.All(slowSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "slow-service"));

        // Search for traces shorter than 50ms AND from fast-service
        var fastSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            TraceDuration = new TraceDurationFilter
                            {
                                MaxNanos = 50_000_000 // 50ms
                            }
                        },
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "fast-service"
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(fastSearch.Results);
        Assert.Contains(fastSearch.Results, r => r.Trace.Spans.Any(s => s.ServiceName == "fast-service"));
    }

    [Fact]
    public async Task SearchTraces_ResourceAttributeFilter_ReturnsMatchingTraces()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        // Create trace with production environment
        var prodTraceId = Enumerable.Range(30, 16).Select(i => (byte)i).ToArray();
        var prodSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 30)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "prod-service" } },
                            new KeyValue { Key = "deployment.environment", Value = new AnyValue { StringValue = "production" } },
                            new KeyValue { Key = "host.name", Value = new AnyValue { StringValue = "prod-host-01" } }
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
                                    TraceId = ByteString.CopyFrom(prodTraceId),
                                    SpanId = ByteString.CopyFrom(prodSpanId),
                                    Name = "process",
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 2_000_000
                                }
                            }
                        }
                    }
                }
            }
        });

        // Create trace with staging environment
        var stagingTraceId = Enumerable.Range(40, 16).Select(i => (byte)i).ToArray();
        var stagingSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 40)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "staging-service" } },
                            new KeyValue { Key = "deployment.environment", Value = new AnyValue { StringValue = "staging" } }
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
                                    TraceId = ByteString.CopyFrom(stagingTraceId),
                                    SpanId = ByteString.CopyFrom(stagingSpanId),
                                    Name = "process",
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 2_000_000
                                }
                            }
                        }
                    }
                }
            }
        });

        var prodTraceIdHex = Convert.ToHexString(prodTraceId);
        var stagingTraceIdHex = Convert.ToHexString(stagingTraceId);
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == prodTraceIdHex)),
            "prod trace to be queryable");
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == stagingTraceIdHex)),
            "staging trace to be queryable");

        // Search for production environment with service name to isolate test data
        var prodSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "prod-service"
                            }
                        },
                        new TraceFilterExpression
                        {
                            Attribute = new AttributeFilter
                            {
                                Key = "deployment.environment",
                                Value = "production",
                                Target = AttributeFilterTarget.Resource
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(prodSearch.Results);
        Assert.All(prodSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "prod-service"));

        // Search for traces with host.name resource attribute (exists check) from prod-service
        var hostSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "prod-service"
                            }
                        },
                        new TraceFilterExpression
                        {
                            Attribute = new AttributeFilter
                            {
                                Key = "host.name",
                                Target = AttributeFilterTarget.Resource,
                                Operator = AttributeFilterOperator.Exists
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(hostSearch.Results);
        Assert.All(hostSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "prod-service"));
    }

    [Fact]
    public async Task SearchTraces_CompositeFilter_SpanKindAndDuration()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        // Create slow CLIENT span
        var slowClientTraceId = Enumerable.Range(50, 16).Select(i => (byte)i).ToArray();
        var slowClientSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 50)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "slow-client" } }
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
                                    TraceId = ByteString.CopyFrom(slowClientTraceId),
                                    SpanId = ByteString.CopyFrom(slowClientSpanId),
                                    Name = "slow-request",
                                    Kind = Span.Types.SpanKind.Client,
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 101_000_000 // 100ms
                                }
                            }
                        }
                    }
                }
            }
        });

        // Create fast CLIENT span
        var fastClientTraceId = Enumerable.Range(60, 16).Select(i => (byte)i).ToArray();
        var fastClientSpanId = Enumerable.Range(1, 8).Select(i => (byte)(i + 60)).ToArray();

        await traceClient.ExportAsync(new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "fast-client" } }
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
                                    TraceId = ByteString.CopyFrom(fastClientTraceId),
                                    SpanId = ByteString.CopyFrom(fastClientSpanId),
                                    Name = "fast-request",
                                    Kind = Span.Types.SpanKind.Client,
                                    StartTimeUnixNano = 1_000_000,
                                    EndTimeUnixNano = 2_000_000 // 1ms
                                }
                            }
                        }
                    }
                }
            }
        });

        var slowClientTraceIdHex = Convert.ToHexString(slowClientTraceId);
        var fastClientTraceIdHex = Convert.ToHexString(fastClientTraceId);
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == slowClientTraceIdHex)),
            "slow client trace to be queryable");
        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == fastClientTraceIdHex)),
            "fast client trace to be queryable");

        // Search for slow CLIENT spans from slow-client service (composite AND filter)
        var compositeSearch = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter
                            {
                                Name = "slow-client"
                            }
                        },
                        new TraceFilterExpression
                        {
                            SpanKind = new SpanKindFilter
                            {
                                Kind = SpanKindFilter.Types.SpanKind.Client
                            }
                        },
                        new TraceFilterExpression
                        {
                            Duration = new DurationFilter
                            {
                                MinNanos = 50_000_000 // 50ms
                            }
                        }
                    }
                }
            },
            Limit = 10
        });

        Assert.Single(compositeSearch.Results);
        Assert.All(compositeSearch.Results, r => Assert.Contains(r.Trace.Spans, s => s.ServiceName == "slow-client"));
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, string failureMessage)
    {
        var timeoutAt = DateTime.UtcNow + DefaultTimeout;
        Exception? lastException = null;
        var delay = 100;

        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                if (await predicate())
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(delay);
            // Exponential backoff up to 1 second
            delay = Math.Min(delay * 2, 1000);
        }

        var message = $"Timed out waiting for {failureMessage}";
        if (lastException is not null)
        {
            throw new TimeoutException(message, lastException);
        }

        throw new TimeoutException(message);
    }
}
