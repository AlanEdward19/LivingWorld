namespace LivingWorld.Domain.Shared;

public enum ResolutionResult
{
    CriticalFailure,
    Failure,
    PartialSuccess,
    Success,
    CriticalSuccess,
}

/// <summary>O "d20" do projeto (ADR-0011): primitivo único usado por todo sistema que decide
/// algo incerto. Todo sorteio vem do <see cref="WorldRng"/> do mundo, nunca de RNG solto.</summary>
public static class Resolver
{
    public static ResolutionResult Resolve(int difficulty, int modifiers, VarianceProfile profile, WorldRng rng)
    {
        var roll = Draw(profile.Kind, rng);
        int margin = roll.Total + modifiers - difficulty;

        if (profile.Kind == VarianceProfileKind.Dramatico)
        {
            if (roll.IsNatural1) return ResolutionResult.CriticalFailure;
            if (roll.IsNatural20) return ResolutionResult.CriticalSuccess;
        }
        else if (profile.Kind == VarianceProfileKind.Raro && roll.IsTailEvent)
        {
            return margin >= 0 ? ResolutionResult.CriticalSuccess : ResolutionResult.CriticalFailure;
        }

        if (margin >= profile.SuccessMargin) return ResolutionResult.Success;
        if (margin >= -profile.PartialMargin) return ResolutionResult.PartialSuccess;
        return ResolutionResult.Failure;
    }

    private readonly record struct Roll(int Total, bool IsNatural1, bool IsNatural20, bool IsTailEvent);

    private static Roll Draw(VarianceProfileKind kind, WorldRng rng) => kind switch
    {
        VarianceProfileKind.Dramatico => RollD20(rng),
        VarianceProfileKind.Agregado => RollAgregado(rng),
        VarianceProfileKind.Raro => RollRaro(rng),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Roll RollD20(WorldRng rng)
    {
        int d20 = (int)(rng.NextDouble() * 20) + 1;
        return new Roll(d20, d20 == 1, d20 == 20, false);
    }

    /// <summary>Soma de 3 uniformes: curva concentrada no meio (aproxima central limit),
    /// sem nenhum ramo de crítico — o perfil Agregado nunca lê IsNatural1/20/IsTailEvent.</summary>
    private static Roll RollAgregado(WorldRng rng)
    {
        double sum = 0;
        for (int i = 0; i < 3; i++) sum += rng.NextDouble() * 6;
        return new Roll((int)sum + 3, false, false, false);
    }

    /// <summary>Cauda longa: evento raro (4% dos sorteios) pode virar crítico; caso contrário
    /// se comporta como um d20 comum.</summary>
    private static Roll RollRaro(WorldRng rng)
    {
        double tailDraw = rng.NextDouble();
        bool isTail = tailDraw is < 0.02 or > 0.98;
        int d20 = (int)(rng.NextDouble() * 20) + 1;
        return new Roll(d20, false, false, isTail);
    }
}
