using Asynkron.OtelReceiver.Data;
using Grpc.Core;
using OtelMcp.Proto.V1;

namespace Asynkron.OtelReceiver.Services;

/// <summary>
/// Exposes TraceLens search and metadata operations over gRPC so external tools can query stored telemetry.
/// </summary>
public class DataServiceImpl(ModelRepo modelRepo) : DataService.DataServiceBase
{
    public override Task<GetSearchDataResponse> GetSearchData(GetSearchDataRequest request, ServerCallContext context)
    {
        return modelRepo.GetSearchData(request);
    }

    public override Task<GetValuesForTagResponse> GetValuesForTag(GetValuesForTagRequest request,
        ServerCallContext context)
    {
        return modelRepo.GetValuesForTag(request);
    }

    public override Task<GetMetricNamesResponse> GetMetricNames(GetMetricNamesRequest request,
        ServerCallContext context)
    {
        return modelRepo.GetMetricNames();
    }

    public override Task<GetMetricResponse> GetMetric(GetMetricRequest request, ServerCallContext context)
    {
        return modelRepo.GetMetric(request);
    }

    public override Task<SearchTraceResponse> SearchTraces(SearchTracesRequest request, ServerCallContext context)
    {
        return modelRepo.SearchTraces(request);
    }

    public override Task<GetTraceResponse> GetTrace(GetTraceRequest request, ServerCallContext context)
    {
        return modelRepo.GetTrace(request);
    }

    public override Task<GetRandomTraceResponse> GetRandomTrace(GetRandomTraceRequest request, ServerCallContext context)
    {
        return modelRepo.GetRandomTrace(request);
    }
}