namespace JsonApiKit;

/// <summary>Non-standard but conventional pagination meta: total resources and page count.</summary>
public sealed record JsonApiMeta(int Total, int PageCount);
