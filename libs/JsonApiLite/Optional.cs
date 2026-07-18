using System.Text.Json.Serialization;

namespace JsonApiLite;

internal interface IOptional
{
    bool IsSet { get; }
}

/// <summary>An attribute value that distinguishes "the member was absent" from "the member was
/// explicitly null" — the tri-state relationships get, one level down. Opt in per attribute by
/// declaring the member as <c>Optional&lt;T&gt;</c> where an explicit null is meaningful: an
/// unset member is omitted when writing and stays unset when the document does not carry it,
/// while an explicit null arrives as set with a null <see cref="Value"/>. Plain members remain
/// the right choice when null never needs to reach the server. Unset omission is applied by
/// <see cref="JsonApiSerializer"/> options.</summary>
[JsonConverter(typeof(OptionalConverterFactory))]
public readonly record struct Optional<T> : IOptional
{
    private Optional(T? value)
    {
        IsSet = true;
        Value = value;
    }

    /// <summary>Whether the document carried the member at all; false means keep the current value.</summary>
    public bool IsSet { get; }

    /// <summary>The carried value when <see cref="IsSet"/>; null means the document explicitly
    /// set the member to null.</summary>
    public T? Value { get; }

    public static Optional<T> None => default;

    public static Optional<T> Of(T? value) => new(value);

    public static implicit operator Optional<T>(T? value) => new(value);
}
