namespace Encargos.Core;

/// <summary>Resultado de una acción: correcto con valor, o un mensaje de error para mostrar.</summary>
public sealed class Res<T>
{
    public bool Ok { get; }
    public T? Value { get; }
    public string Error { get; }
    Res(bool ok, T? v, string e) { Ok = ok; Value = v; Error = e; }
    public static Res<T> Success(T v) => new(true, v, "");
    public static Res<T> Fail(string e) => new(false, default, e);
    public T GetOrThrow() => Ok ? Value! : throw new InvalidOperationException(Error);
}
public static class Res
{
    public static Res<bool> Ok() => Res<bool>.Success(true);
    public static Res<bool> Fail(string e) => Res<bool>.Fail(e);
}
