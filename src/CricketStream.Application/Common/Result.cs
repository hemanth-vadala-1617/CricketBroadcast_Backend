namespace CricketStream.Application.Common;

public enum ErrorKind { Validation, NotFound, Conflict, Unprocessable }

public sealed record Error(ErrorKind Kind, string Message);

/// <summary>Predictable failures (bad input, wrong match state, stale version) are returned, never thrown.</summary>
public class Result<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public Error? Error { get; private init; }

    public static Result<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static Result<T> Fail(ErrorKind kind, string message) => new() { Error = new Error(kind, message) };
    public static Result<T> Fail(Error error) => new() { Error = error };

    public static implicit operator Result<T>(T value) => Ok(value);
}

public static class Fail
{
    public static Error Validation(string m) => new(ErrorKind.Validation, m);
    public static Error NotFound(string m) => new(ErrorKind.NotFound, m);
    public static Error Conflict(string m) => new(ErrorKind.Conflict, m);
    public static Error Unprocessable(string m) => new(ErrorKind.Unprocessable, m);
}
