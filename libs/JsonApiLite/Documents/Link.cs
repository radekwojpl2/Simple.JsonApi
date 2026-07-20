using System.Text.Json.Serialization;

namespace JsonApiLite;

/// <summary>One link: a bare URI string on the wire when it carries no metadata, a link object
/// ({"href": ..., "meta": ...}) when it does (https://jsonapi.org/format/#document-links).
/// Implicitly converts from string, so links still read as plain assignments.</summary>
[JsonConverter(typeof(LinkConverter))]
public sealed record Link(string Href)
{
    public Meta? Meta { get; init; }

    public static implicit operator Link?(string? href)
    {
        if (href is null)
        {
            return null;
        }
        return new Link(href);
    }
}
