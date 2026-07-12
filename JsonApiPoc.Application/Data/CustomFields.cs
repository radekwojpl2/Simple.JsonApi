using System.Globalization;
using System.Text.Json;
using JsonApiPoc.Application.Common;
using JsonApiPoc.Domain;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Data;

public static class CustomFields
{
    /// <summary>Loads all custom field values for the given resources, keyed by resource id, with values typed per their definition.</summary>
    public static async Task<Dictionary<int, Dictionary<string, object?>>> LoadAsync(
        AppDbContext db, string resourceType, IReadOnlyCollection<int> resourceIds, CancellationToken cancellationToken)
    {
        var values = await db.CustomFieldValues.AsNoTracking()
            .Include(v => v.Definition)
            .Where(v => v.ResourceType == resourceType && resourceIds.Contains(v.ResourceId))
            .ToListAsync(cancellationToken);

        return values
            .GroupBy(v => v.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(v => v.Definition!.Key, v => ParseValue(v.Definition!, v.Value)));
    }

    /// <summary>Validates incoming custom field values against the definitions for the resource type.
    /// Returns an error, or the converted (definitionId, rawValue) pairs ready to store.</summary>
    public static async Task<(CommandError? Error, List<(int DefinitionId, string Value)> Converted)> ValidateAsync(
        AppDbContext db, string resourceType, Dictionary<string, JsonElement>? fields, CancellationToken cancellationToken)
    {
        var converted = new List<(int, string)>();
        if (fields is null || fields.Count == 0)
        {
            return (null, converted);
        }

        var definitions = await db.CustomFieldDefinitions
            .Where(d => d.ResourceType == resourceType)
            .ToDictionaryAsync(d => d.Key, cancellationToken);

        foreach (var (key, value) in fields)
        {
            if (!definitions.TryGetValue(key, out var definition))
            {
                return (new CommandError(422, "Validation failed",
                    $"Unknown custom field '{key}' for resource type '{resourceType}'."), converted);
            }

            var raw = ToRawValue(definition, value);
            if (raw is null)
            {
                return (new CommandError(422, "Validation failed",
                    $"Custom field '{key}' expects a {definition.DataType} value."), converted);
            }

            converted.Add((definition.Id, raw));
        }

        return (null, converted);
    }

    public static void Attach(AppDbContext db, string resourceType, int resourceId,
        IEnumerable<(int DefinitionId, string Value)> converted)
    {
        foreach (var (definitionId, value) in converted)
        {
            db.CustomFieldValues.Add(new CustomFieldValue
            {
                DefinitionId = definitionId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Value = value
            });
        }
    }

    /// <summary>Updates existing value rows for the resource and inserts rows for fields it does not have yet.</summary>
    public static async Task UpsertAsync(AppDbContext db, string resourceType, int resourceId,
        IReadOnlyCollection<(int DefinitionId, string Value)> converted, CancellationToken cancellationToken)
    {
        if (converted.Count == 0)
        {
            return;
        }

        var existing = await db.CustomFieldValues
            .Where(v => v.ResourceType == resourceType && v.ResourceId == resourceId)
            .ToListAsync(cancellationToken);

        foreach (var (definitionId, value) in converted)
        {
            var row = existing.FirstOrDefault(v => v.DefinitionId == definitionId);
            if (row is null)
            {
                db.CustomFieldValues.Add(new CustomFieldValue
                {
                    DefinitionId = definitionId,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Value = value
                });
            }
            else
            {
                row.Value = value;
            }
        }
    }

    private static object? ParseValue(CustomFieldDefinition definition, string raw) => definition.DataType switch
    {
        "number" => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : raw,
        "boolean" => bool.TryParse(raw, out var flag) ? flag : raw,
        _ => raw
    };

    private static string? ToRawValue(CustomFieldDefinition definition, JsonElement value) => definition.DataType switch
    {
        "text" when value.ValueKind == JsonValueKind.String => value.GetString(),
        "date" when value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out _) => value.GetString(),
        "number" when value.ValueKind == JsonValueKind.Number => value.GetDecimal().ToString(CultureInfo.InvariantCulture),
        "boolean" when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean() ? "true" : "false",
        _ => null
    };
}
