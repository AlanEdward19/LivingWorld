namespace LivingWorld.Domain;

/// <summary>Estágio de vida do NPC (Fase 4, task 3), usado pela rotina diária
/// (<c>ActionCatalog.RoutineOf</c>) — nunca a idade bruta.</summary>
public enum LifeStage
{
    Child,
    Adult,
    Elder,
}

/// <summary>Limiares de idade que resolvem <see cref="LifeStage"/> — dado do cenário, nunca
/// constante em C# (R3), mesmo padrão de <see cref="PopulationRules"/>.</summary>
public sealed record LifeStageRules(int ChildMaxAge, int AdultMaxAge)
{
    public static Result<LifeStageRules> Create(int childMaxAge, int adultMaxAge)
    {
        if (childMaxAge < 0)
            return Result<LifeStageRules>.Fail("ChildMaxAge: deve ser >= 0");
        if (adultMaxAge <= childMaxAge)
            return Result<LifeStageRules>.Fail("AdultMaxAge: deve ser > ChildMaxAge");

        return Result<LifeStageRules>.Ok(new LifeStageRules(childMaxAge, adultMaxAge));
    }

    public LifeStage LifeStageOf(int ageYears)
    {
        if (ageYears <= ChildMaxAge) return LifeStage.Child;
        if (ageYears <= AdultMaxAge) return LifeStage.Adult;
        return LifeStage.Elder;
    }
}
