namespace PAOS.Tests;

// All [Collection("Integration")] classes share one ApiFactory and run sequentially,
// preventing parallel DB conflicts.
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<ApiFactory> { }
