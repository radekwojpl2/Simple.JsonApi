namespace JsonApiPoc.Specification.IntegrationTests;

/// <summary>xUnit resolves collection definitions per assembly, so this assembly needs its own
/// definition even though it reuses <see cref="ApiCollection.Name"/> and <see cref="ApiFactory"/>
/// from the endpoint-test project. One container + one host for the whole run, and the collection
/// serializes the test classes so the per-test database reset can't race another class.</summary>
[CollectionDefinition(ApiCollection.Name)]
public sealed class SpecificationApiCollection : ICollectionFixture<ApiFactory>;
