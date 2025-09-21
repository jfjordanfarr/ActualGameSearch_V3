namespace ActualGameSearch.Core.Primitives;

public sealed record Error(string Code, string Message, object? Details = null);

public sealed record Result<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public Error? Error { get; init; }

    public static Result<T> Success(T data) => new() { Ok = true, Data = data };
    public static Result<T> Fail(string code, string message, object? details = null) => new() { Ok = false, Error = new Error(code, message, details) };
    public static Result<T> Fail(Error error) => new() { Ok = false, Error = error };
}
