namespace JsonApiKit;

public sealed class JsonApiOptions
{
    public int DefaultPageSize { get; set; } = 25;

    public int MaxPageSize { get; set; } = 100;

    internal List<Type> MapTypes { get; } = [];

    public JsonApiOptions AddMap<TMap>() where TMap : class, IResourceMap
    {
        MapTypes.Add(typeof(TMap));
        return this;
    }
}
