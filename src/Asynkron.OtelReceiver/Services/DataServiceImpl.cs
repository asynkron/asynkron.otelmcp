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

    public override Task<SearchTracesResponse> SearchTraces(SearchTracesRequest request, ServerCallContext context)
    {
        return modelRepo.SearchTraces(request);
    }

    public override Task<GetServiceMapComponentsResponse> GetServiceMapComponents(
        GetServiceMapComponentsRequest request, ServerCallContext context)
    {
        return modelRepo.GetServiceMapComponents(request);
    }

    public override Task<GetComponentMetadataResponse> GetComponentMetadata(GetComponentMetadataRequest request,
        ServerCallContext context)
    {
        return modelRepo.GetComponentMetadata();
    }

    public override Task<SetComponentMetadataResponse> SetComponentMetadata(SetComponentMetadataRequest request,
        ServerCallContext context)
    {
        return modelRepo.SetComponentMetadata(request);
    }

    public override Task<GetMetadataForComponentResponse> GetMetadataForComponent(
        GetMetadataForComponentRequest request, ServerCallContext context)
    {
        return modelRepo.GetMetadataForComponent(request);
    }

    public override Task<GetSnapshotResponse> GetSnapshot(GetSnapshotRequest request, ServerCallContext context)
    {
        return modelRepo.GetSnapshot(request);
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
}