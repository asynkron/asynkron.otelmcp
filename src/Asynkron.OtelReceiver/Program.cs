using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Asynkron.OtelReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
        // Allow both HTTP/1.1 and HTTP/2 so JSON transcoded HTTP endpoints can coexist with gRPC.
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2);
});

var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                             ?? "Data Source=otel.db";

builder.Services.AddDbContextFactory<OtelReceiverContext>(options => { options.UseSqlite(sqliteConnectionString); });

builder.Services.AddScoped<ISpanBulkInserter, SqliteSpanBulkInserter>();

builder.Services.AddSingleton<IReceiverMetricsCollector, ReceiverMetricsCollector>();
builder.Services.AddGrpc();
    //.AddJsonTranscoding();
builder.Services.AddScoped<ModelRepo>();

var app = builder.Build();

app.MapGrpcService<TraceServiceImpl>();
app.MapGrpcService<LogsServiceImpl>();
app.MapGrpcService<MetricsServiceImpl>();
app.MapGrpcService<ReceiverMetricsServiceImpl>();
app.MapGrpcService<DataServiceImpl>();
app.MapMcpStreamingEndpoint();
app.MapGet("/", () => "Asynkron Otel Receiver");

await using (var scope = app.Services.CreateAsyncScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

await app.RunAsync();