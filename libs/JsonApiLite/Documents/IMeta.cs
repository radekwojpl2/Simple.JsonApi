namespace JsonApiLite;

/// <summary>Marks a record as a meta shape. Empty on purpose, like <see cref="IAttributes"/> and
/// <see cref="IRelationships"/>. The built-in <see cref="Meta"/> implements it, so leaving a
/// document's meta type unspoken still satisfies the constraint.</summary>
public interface IMeta;
