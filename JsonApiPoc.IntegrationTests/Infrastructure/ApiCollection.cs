namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>One container + one host for the whole run; the collection also serializes the test
/// classes, so the per-test database reset in <see cref="ApiTestBase"/> can't race a test in
/// another class.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
