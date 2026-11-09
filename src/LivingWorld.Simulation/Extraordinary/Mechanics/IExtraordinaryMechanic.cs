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

/// <summary>
/// Um token que uma mecânica aceita: <see cref="Head"/> literal (<c>npc.health</c>) ou prefixo
/// aberto terminado em <c>.</c> ou <c>:</c> (<c>area:radius:</c>); <see cref="Format"/> é o
/// formato legível para editores; <see cref="Target"/> diz quem sofre (<c>target</c>,
/// <c>carrier</c>, <c>area</c>, <c>world</c>, <c>household</c>). <see cref="Syntax"/> é a regra
/// sem contexto de mundo (magnitude, sinal, sufixo, formato), declarada junto da mecânica e,
/// quando possível, o mesmo parser que a invocação usa; estado (saldo, NPC existente) fica na invocação.
/// </summary>
public sealed record ExtraordinaryPrimitive(
    string Head, string Format, bool SignedMagnitude, string Target, Func<string, bool> Syntax,
    bool LooseHead = false)
{
    /// <summary>Aceita o token como a invocação o veria: direto, ou reduzido da forma tipada
    /// <c>chave:tipo:n</c> para <c>chave:n</c> (o motor faz essa redução para toda mecânica).</summary>
    public bool Matches(string token) =>
        Check(token)
        || ExtraordinaryMechanicSupport.TrySplitTypedMagnitude(token, out string stripped, out _) && Check(stripped);

    private bool Check(string token)
    {
        if (!token.StartsWith(Head, StringComparison.Ordinal))
            return false;
        char last = Head[^1];
        bool headOk = LooseHead || last is '.' or ':' || token.Length == Head.Length || token[Head.Length] == ':';
        return headOk && Syntax(token);
    }
}

/// <summary>Regras de sintaxe reutilizáveis para <see cref="ExtraordinaryPrimitive.Syntax"/>.</summary>
public static class PrimitiveSyntax
{
    /// <summary>Token exato, sem argumentos.</summary>
    public static Func<string, bool> Flag(string token) =>
        candidate => string.Equals(candidate, token, StringComparison.Ordinal);

    /// <summary><c>chave:n</c> com magnitude não zero (positiva se não assinada). A forma tipada
    /// <c>chave:tipo:n</c> é reduzida em <see cref="ExtraordinaryPrimitive.Matches"/>.</summary>
    public static Func<string, bool> Amount(string key, bool signed) =>
        AmountWith(candidate => string.Equals(candidate, key, StringComparison.Ordinal), signed);

    public static Func<string, bool> AmountWith(Func<string, bool> keyOk, bool signed) =>
        token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", signed);
            return parsed.IsSuccess && keyOk(parsed.Value.Key);
        };

    /// <summary>Mecânicas que só leem a magnitude (<c>ParseAmount</c>) e não conferem a chave.</summary>
    public static Func<string, bool> AnyKeyAmount(bool signed) =>
        token => ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", signed).IsSuccess;

    /// <summary><c>chave:n</c> sem forma tipada.</summary>
    public static Func<string, bool> AmountExact(string key, bool signed) =>
        token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", signed);
            return parsed.IsSuccess && string.Equals(parsed.Value.Key, key, StringComparison.Ordinal);
        };

    /// <summary><c>chave:x</c> com x decimal positivo (cultura invariante).</summary>
    public static Func<string, bool> PositiveNumber(string key) =>
        token =>
        {
            string prefix = key + ":";
            return token.StartsWith(prefix, StringComparison.Ordinal)
                && double.TryParse(token[prefix.Length..], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double value)
                && value > 0 && double.IsFinite(value);
        };

    /// <summary>Prefixo seguido de argumento não vazio.</summary>
    public static Func<string, bool> NonEmptyArgument(string prefix) =>
        token => token.StartsWith(prefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(token[prefix.Length..]);
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
