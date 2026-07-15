using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace JsonApiKit;

/// <summary>Endpoint metadata declaring a JSON:API resource-document write body whose attributes
/// are typed as <see cref="AttributesType"/>. Read by JsonApiKit.OpenApi to render a real
/// request-body schema — the endpoint binds JsonNode, so without this the generated document
/// shows an untyped body.</summary>
public sealed record ResourceDocumentBodyMetadata(string ResourceType, Type AttributesType,
    bool RequiresId, IReadOnlyList<ResourceDocumentRelationshipMetadata> Relationships);

/// <summary>One to-one relationship the resource document may carry. <see cref="Required"/> means
/// the document must provide it (creates); <see cref="Clearable"/> means data may be null.</summary>
public sealed record ResourceDocumentRelationshipMetadata(string Name, string TargetType,
    bool Required, bool Clearable);

public static class ResourceDocumentEndpointExtensions
{
    /// <summary>Declares the endpoint's request body as a JSON:API resource document with
    /// <typeparamref name="TAttributes"/> attributes: sets the accepted content type and the
    /// metadata JsonApiKit.OpenApi turns into the document schema. <paramref name="update"/>
    /// marks PATCH bodies, whose resource object must also carry the endpoint's id.</summary>
    public static RouteHandlerBuilder WithResourceDocumentBody<TAttributes>(
        this RouteHandlerBuilder builder, string resourceType, bool update,
        params ResourceDocumentRelationshipMetadata[] relationships) where TAttributes : notnull =>
        builder
            .Accepts<TAttributes>(JsonApiResults.MediaType)
            .WithMetadata(new ResourceDocumentBodyMetadata(resourceType,
                typeof(TAttributes), RequiresId: update, relationships));
}
