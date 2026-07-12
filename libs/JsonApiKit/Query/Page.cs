namespace JsonApiKit;

/// <summary>1-based JSON:API page request parsed from ?page[number]=&amp;page[size]=.</summary>
public sealed record Page(int Number, int Size);
