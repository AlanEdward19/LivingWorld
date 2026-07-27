namespace LivingWorld.Domain;

/// <summary>Sentinela "sucesso sem valor" para <c>Result&lt;Unit&gt;</c> — operações que só
/// precisam expressar falha nomeada (ex.: <see cref="Workplace.Hire"/>), sem devolver dado
/// nenhum no caminho feliz.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

/// <summary>Erro de negócio explícito em vez de null/exceção.</summary>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
