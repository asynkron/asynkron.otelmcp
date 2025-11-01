using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[CollectionDefinition("SearchFilterIntegration", DisableParallelization = true)]
public class SearchFilterIntegrationCollection : ICollectionFixture<OtelReceiverApplicationFactory>
{
}
