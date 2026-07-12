namespace JsonApiPoc.Application.Common;

/// <summary>Transport-agnostic failure: the presentation layer decides how to render it (JSON:API error document, ProblemDetails, ...).</summary>
public sealed record CommandError(int Status, string Title, string Detail);

public sealed record CommandResult<T>(T? Value, CommandError? Error) where T : class
{
    public static CommandResult<T> Ok(T value) => new(value, null);
    public static CommandResult<T> Fail(int status, string title, string detail) => new(null, new CommandError(status, title, detail));
}
