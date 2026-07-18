namespace JsonApiLite.Tests;

public sealed record PatchDealAttributes : IResourceType
{
    public static string ResourceType => "deals";

    public Optional<string?> Title { get; init; }
    public Optional<decimal?> Amount { get; init; }
    public Optional<string?> Stage { get; init; }
}

/// <summary>Attribute-level tri-state via <see cref="Optional{T}"/>: absent (keep the current
/// value), explicit null (clear), and a value — the distinction plain nullable members cannot
/// carry.</summary>
public class OptionalAttributeTests
{
    [Fact]
    public void Distinguishes_absent_from_explicit_null_on_read()
    {
        var document = Wire.Roundtrip(new ResourceDocument<PatchDealAttributes>
        {
            Data = new Resource<PatchDealAttributes>
            {
                Type = PatchDealAttributes.ResourceType,
                Id = "42",
                Attributes = new PatchDealAttributes
                {
                    Title = Optional<string?>.Of(null),
                    Stage = "won",
                },
            },
        });

        var attributes = document.Data!.Attributes!;
        Assert.True(attributes.Title.IsSet);
        Assert.Null(attributes.Title.Value);
        Assert.True(attributes.Stage.IsSet);
        Assert.Equal("won", attributes.Stage.Value);
        Assert.False(attributes.Amount.IsSet);
    }

    [Fact]
    public void An_omitted_member_stays_unset_through_the_round_trip()
    {
        var document = new ResourceDocument<PatchDealAttributes>
        {
            Data = new Resource<PatchDealAttributes>
            {
                Type = PatchDealAttributes.ResourceType,
                Id = "42",
                Attributes = new PatchDealAttributes { Stage = "won" },
            },
        };

        var json = JsonApiSerializer.Serialize(document);
        Assert.Equal(
            """{"data":{"type":"deals","id":"42","attributes":{"stage":"won"}}}""",
            json);

        var attributes = JsonApiSerializer.Deserialize<ResourceDocument<PatchDealAttributes>>(json)!
            .Data!.Attributes!;
        Assert.False(attributes.Title.IsSet);
        Assert.True(attributes.Stage.IsSet);
        Assert.Equal("won", attributes.Stage.Value);
    }

    [Fact]
    public void Writes_set_members_only_including_explicit_nulls()
    {
        var document = new ResourceDocument<PatchDealAttributes>
        {
            Data = new Resource<PatchDealAttributes>
            {
                Type = PatchDealAttributes.ResourceType,
                Id = "42",
                Attributes = new PatchDealAttributes
                {
                    Title = Optional<string?>.Of(null),
                    Stage = "won",
                },
            },
        };

        Assert.Equal(
            """{"data":{"type":"deals","id":"42","attributes":{"title":null,"stage":"won"}}}""",
            JsonApiSerializer.Serialize(document));
    }

    [Fact]
    public void Optional_attributes_survive_a_round_trip()
    {
        var attributes = new PatchDealAttributes
        {
            Title = Optional<string?>.Of(null),
            Amount = 99.5m,
        };
        var document = new ResourceDocument<PatchDealAttributes>
        {
            Data = new Resource<PatchDealAttributes> { Type = PatchDealAttributes.ResourceType, Id = "42", Attributes = attributes },
        };

        var json = JsonApiSerializer.Serialize(document);
        var reread = JsonApiSerializer.Deserialize<ResourceDocument<PatchDealAttributes>>(json)!;

        Assert.Equal(attributes, reread.Data!.Attributes);
    }
}
