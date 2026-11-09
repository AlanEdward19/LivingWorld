using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public enum ExtraordinaryMechanicKind
{
    Effect,
    Cost,
}

public sealed record ExtraordinaryMechanicContext(
    WorldState World,
    TickContext Tick,
    ExtraordinaryInvocation Invocation,
    Npc Carrier,
    Npc Target,
    ExtraordinaryMechanicKind Kind);

public sealed record PreparedMutation(string Token, Action<ResolutionResult> Apply);

/// <summary>Uma linha do catálogo: <see cref="Head"/> literal (<c>npc.health</c>) ou prefixo aberto
/// terminado em <c>.</c> ou <c>:</c> (<c>area:radius:</c>); <see cref="Format"/> é o formato legível
/// para editores; <see cref="Target"/> diz quem sofre (<c>target</c>, <c>carrier</c>, <c>area</c>,
/// <c>world</c>, <c>household</c>).</summary>
public readonly record struct PrimitiveListing(string Head, string Format, bool SignedMagnitude, string Target);

/// <summary>Parser tipado e sem efeito de um token (ADR-0040): erro com a mensagem da invocação.</summary>
public delegate Result<T> PrimitiveParser<T>(string token);

/// <summary>
/// Um token que uma mecânica aceita, com UM parser tipado (<see cref="ExtraordinaryPrimitive{T}.Parse"/>).
/// A carga do cenário (<see cref="Accepts"/>/<see cref="Matches"/>) e a invocação (<c>Prepare*</c> e
/// os leitores de efeito) chamam esse mesmo parser; o que depende do mundo (NPC, Fact, saldo, mapa)
/// fica só na invocação. <see cref="Listing"/> é o que o catálogo mostra (vazio = token tolerado
/// que a invocação aceita sem efeito, registrado no ADR-0040).
/// </summary>
public abstract class ExtraordinaryPrimitive
{
    protected ExtraordinaryPrimitive(
        string head, bool looseHead, bool allowTyped, IReadOnlyList<PrimitiveListing> listing)
    {
        Head = head;
        LooseHead = looseHead;
        AllowTyped = allowTyped;
        Listing = listing;
    }

    public string Head { get; }
    public bool LooseHead { get; }

    /// <summary>O motor reduz <c>chave:tipo:n</c> para <c>chave:n</c> antes de resolver; seletor de
    /// área lê o token cru e não passa por essa redução.</summary>
    public bool AllowTyped { get; }

    public IReadOnlyList<PrimitiveListing> Listing { get; }

    /// <summary>Aceita o token exatamente como a mecânica o recebe.</summary>
    public abstract bool Accepts(string token);

    /// <summary>Aceita o token como a invocação o veria: direto, ou reduzido da forma tipada.</summary>
    public bool Matches(string token) =>
        Accepts(token)
        || AllowTyped && ExtraordinaryMechanicSupport.TrySplitTypedMagnitude(token, out string stripped, out _)
            && Accepts(stripped);
}

public sealed class ExtraordinaryPrimitive<T> : ExtraordinaryPrimitive
{
    private readonly PrimitiveParser<T> _parser;

    public ExtraordinaryPrimitive(
        string head, string format, bool signedMagnitude, string target, PrimitiveParser<T> parser,
        bool looseHead = false, bool allowTyped = true)
        : this(head, [new PrimitiveListing(head, format, signedMagnitude, target)], parser, looseHead, allowTyped)
    {
    }

    public ExtraordinaryPrimitive(
        string head, IReadOnlyList<PrimitiveListing> listing, PrimitiveParser<T> parser,
        bool looseHead = false, bool allowTyped = true)
        : base(head, looseHead, allowTyped, listing)
    {
        _parser = parser;
    }

    /// <summary>O parser único: fronteira do <see cref="ExtraordinaryPrimitive.Head"/>, sintaxe e valor tipado.</summary>
    public Result<T> Parse(string token)
    {
        if (!token.StartsWith(Head, StringComparison.Ordinal))
            return Result<T>.Fail(HeadMismatch);
        char last = Head[^1];
        bool headOk = LooseHead || last is '.' or ':' || token.Length == Head.Length || token[Head.Length] == ':';
        return headOk ? _parser(token) : Result<T>.Fail(HeadMismatch);
    }

    // Constante: os leitores de efeito chamam Parse em todo token do descritor, sem alocar mensagem.
    private const string HeadMismatch = "Effects: alvo não suportado";

    public bool TryParse(string token, out T value)
    {
        var parsed = Parse(token);
        value = parsed.IsSuccess ? parsed.Value! : default!;
        return parsed.IsSuccess;
    }

    public override bool Accepts(string token) => Parse(token).IsSuccess;
}

/// <summary>Parsers reutilizáveis para <see cref="ExtraordinaryPrimitive{T}"/>.</summary>
public static class PrimitiveSyntax
{
    private static Result<T> Reject<T>(string token) => Result<T>.Fail($"Effects: alvo não suportado '{token}'");

    /// <summary>Token exato, sem argumentos.</summary>
    public static PrimitiveParser<bool> Flag(string token) =>
        candidate => string.Equals(candidate, token, StringComparison.Ordinal)
            ? Result<bool>.Ok(true)
            : Reject<bool>(candidate);

    /// <summary><c>chave:n</c> com magnitude não zero (positiva se não assinada); a chave confere em <paramref name="keyOk"/>.</summary>
    public static PrimitiveParser<(string Key, int Amount)> AmountWith(Func<string, bool> keyOk, bool signed) =>
        token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", signed);
            if (!parsed.IsSuccess) return parsed;
            return keyOk(parsed.Value.Key) ? parsed : Reject<(string, int)>(parsed.Value.Key);
        };

    /// <summary><c>chave:n</c> com a chave exata (a forma tipada <c>chave:tipo:n</c> é reduzida em <see cref="ExtraordinaryPrimitive.Matches"/>).</summary>
    public static PrimitiveParser<(string Key, int Amount)> Amount(string key, bool signed) =>
        AmountWith(candidate => string.Equals(candidate, key, StringComparison.Ordinal), signed);

    /// <summary>Mecânicas que só leem a magnitude (<c>ParseAmount</c>) e não conferem a chave.</summary>
    public static PrimitiveParser<(string Key, int Amount)> AnyKeyAmount(bool signed) =>
        AmountWith(static _ => true, signed);

    /// <summary><c>chave:x</c> com x decimal positivo (cultura invariante, aceita infinito).</summary>
    public static PrimitiveParser<double> PositiveNumber(string key) =>
        PositiveNumberAfter(key + ":");

    private static PrimitiveParser<double> PositiveNumberAfter(string prefix) =>
        token => token.StartsWith(prefix, StringComparison.Ordinal)
            && double.TryParse(token[prefix.Length..], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value)
            && value > 0
            ? Result<double>.Ok(value)
            : Reject<double>(token);

    /// <summary>Prefixo seguido de argumento não vazio (o argumento sem trim: a invocação o lê assim).</summary>
    public static PrimitiveParser<string> NonEmptyArgument(string prefix) =>
        token => token.StartsWith(prefix, StringComparison.Ordinal) && token.Length > prefix.Length
            ? Result<string>.Ok(token[prefix.Length..])
            : Reject<string>(token);

    public delegate bool TryParser<T>(string token, out T value);

    /// <summary>Adapta um <c>bool TryParse(out T)</c> ao parser tipado, com a mensagem da invocação.</summary>
    public static PrimitiveParser<T> From<T>(TryParser<T> tryParse, Func<string, string> error) =>
        token => tryParse(token, out T value) ? Result<T>.Ok(value) : Result<T>.Fail(error(token));

    /// <summary>Aceita qualquer sufixo (token tolerado: a invocação o aceita sem efeito).</summary>
    public static PrimitiveParser<string> Anything() => static token => Result<string>.Ok(token);
}

public interface IExtraordinaryMechanic
{
    string Prefix { get; }
    ExtraordinaryMechanicKind Kind { get; }

    /// <summary>Tokens que esta mecânica aceita. Fonte única do catálogo e da validação na carga.</summary>
    IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; }

    Result<PreparedMutation?> PrepareEffect(ExtraordinaryMechanicContext ctx, string declaration);

    Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key);

    Result<PreparedMutation> PrepareCost(ExtraordinaryMechanicContext ctx, string declaration, int amount);
}

public interface IExtraordinaryMechanicRegistry
{
    IExtraordinaryMechanic? Resolve(string token);
}

public abstract class ExtraordinaryMechanic : IExtraordinaryMechanic
{
    public abstract string Prefix { get; }
    public abstract ExtraordinaryMechanicKind Kind { get; }
    public abstract IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; }

    public virtual Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

    public virtual Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
        => Result<long>.Fail($"Costs: alvo não suportado '{key}'");

    public virtual Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
        => Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{declaration}'");
}
