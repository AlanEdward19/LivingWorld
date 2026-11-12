using LivingWorld.Domain.Health;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Health;

/// <summary>Amamentação (AD-134). Lactente = vivo com menos de <see cref="LactationRules.WeaningEndDays"/> dias. Quem dá o
/// leite: a mãe, se viva, na mesma casa e cidade, não passando fome e ainda com leite (pariu dentro da janela e deu de mamar há
/// menos de <see cref="LactationRules.LapseDays"/> dias); senão a primeira outra lactante da casa, em ordem de id. A fome de
/// quem amamenta decai mais rápido na proporção do leite que dá. Tudo derivado de estado gravado: sem sorteio, sem dicionário.</summary>
public static class Lactation
{
    public static LactationRules Rules(WorldState world) => world.NeedsRules.Physiology.Lactation;

    public static bool Enabled(WorldState world) => world.NeedsRules.Physiology.Enabled && Rules(world).Enabled;

    public static double AgeDays(WorldState world, Npc npc, long tick) =>
        Math.Max(0, tick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerDay;

    /// <summary>Lactente nascido no mundo (o fundador pequeno não teve a amamentação simulada).</summary>
    public static bool IsNursling(WorldState world, Npc npc, long tick) =>
        npc.IsAlive && npc.GestationAtBirthDays is not null && AgeDays(world, npc, tick) < Rules(world).WeaningEndDays;

    /// <summary>A mulher tem leite para dar agora.</summary>
    public static bool CanNurse(WorldState world, Npc woman, long tick)
    {
        if (!woman.IsAlive || woman.Sex != Sex.Female || woman.HungerZeroSinceTick is not null) return false;
        if (woman.Reproduction?.LastBirthTick is not { } birth) return false;
        var rules = Rules(world);
        long day = world.Calendar.HoursPerDay;
        if (tick - birth >= rules.WeaningEndDays * day) return false;
        long lapse = rules.LapseDays * day;
        return tick - birth < lapse || (woman.LastLactationTick is { } last && tick - last < lapse);
    }

    /// <summary>Quem amamenta o lactente agora, ou nulo (sem leite: papa da despensa, com risco).</summary>
    public static Npc? FindCaregiver(WorldState world, Npc baby, long tick)
    {
        if (baby.Household is not { } householdId) return null;
        if (baby.MotherId is { } motherId && world.FindNpc(motherId) is { } mother
            && mother.Household == householdId && mother.City == baby.City && CanNurse(world, mother, tick))
            return mother;
        if (world.FindHousehold(householdId) is not { } household) return null;
        Npc? best = null;
        foreach (var id in household.Members)
        {
            if (id == baby.Id || (best is not null && id.Value >= best.Id.Value)) continue;
            if (world.FindNpc(id) is { } other && other.City == baby.City && CanNurse(world, other, tick)) best = other;
        }

        return best;
    }

    /// <summary>Parte do alimento do lactente que vem do leite na idade dele.</summary>
    public static double MilkShare(WorldState world, Npc baby, long tick) => Rules(world).MilkShare(AgeDays(world, baby, tick));

    /// <summary>Mamada: marca o bebê e quem deu o leite. Devolve se houve leite.</summary>
    public static bool TryNurse(WorldState world, Npc baby, long tick)
    {
        if (!Enabled(world) || !IsNursling(world, baby, tick)) return false;
        if (FindCaregiver(world, baby, tick) is not { } caregiver) return false;
        baby.MarkNursed(tick);
        caregiver.MarkLactation(tick);
        return true;
    }

    /// <summary>Leite que a mulher dá hoje: soma da parte do leite de cada lactente da casa que ela amamenta.</summary>
    public static double NursingLoad(WorldState world, Npc woman, long tick)
    {
        if (woman.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return 0;
        double load = 0;
        foreach (var id in household.Members)
        {
            if (id == woman.Id || world.FindNpc(id) is not { } baby || !IsNursling(world, baby, tick)) continue;
            if (FindCaregiver(world, baby, tick)?.Id == woman.Id) load += MilkShare(world, baby, tick);
        }

        return load;
    }

    /// <summary>Taxa da fome de quem amamenta: a base do cenário vezes (1 + extra × leite dado). Volta à base sem lactente.</summary>
    public static void RefreshHungerCost(WorldState world, Npc woman, long tick)
    {
        if (!woman.IsAlive || woman.Sex != Sex.Female) return;
        double baseRate = world.NeedsRules.HungerDecayPerHour;
        double load = Enabled(world) ? NursingLoad(world, woman, tick) : 0;
        woman.SetHungerDecayRate(baseRate * (1 + Rules(world).HungerDecayExtraPerNursling * load), tick);
    }

    /// <summary>Atualiza o custo de todas as mulheres da casa (nascimento, morte, mudança de quem amamenta).</summary>
    public static void RefreshHousehold(WorldState world, Npc member, long tick)
    {
        if (!Enabled(world) || member.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;
        foreach (var id in household.Members.OrderBy(m => m.Value))
            if (world.FindNpc(id) is { IsAlive: true, Sex: Sex.Female } woman && woman.Reproduction?.LastBirthTick is not null)
                RefreshHungerCost(world, woman, tick);
    }

    /// <summary>Morte de lactente: a mãe que deixa de amamentar volta a ovular em semanas (fim da amenorreia da lactação).
    /// Morte de lactante: os lactentes da casa passam a outra lactante (ou ficam sem leite).</summary>
    public static void OnDeath(WorldState world, Npc dead, long tick)
    {
        if (!Enabled(world)) return;
        if (dead.MotherId is { } motherId && AgeDays(world, dead, tick) < Rules(world).WeaningEndDays
            && world.FindNpc(motherId) is { IsAlive: true } mother)
        {
            RefreshHousehold(world, mother, tick);
            ShortenAmenorrhea(world, mother, tick);
        }

        if (dead.Sex == Sex.Female && dead.Household is not null)
            foreach (var id in world.FindHousehold(dead.Household.Value)?.Members ?? [])
                if (world.FindNpc(id) is { IsAlive: true } other)
                {
                    RefreshHousehold(world, other, tick);
                    break;
                }
    }

    private static void ShortenAmenorrhea(WorldState world, Npc mother, long tick)
    {
        if (mother.Reproduction is not { LastBirthTick: { } birth } history || history.InfecundUntilTick <= tick) return;
        if (NursingLoad(world, mother, tick) > 0) return; // ainda amamenta (gêmeo, outro lactente)
        var rules = Rules(world);
        long day = world.Calendar.HoursPerDay;
        long until = Math.Max(birth + rules.MinPostpartumInfecundDaysWithoutNursing * day, tick + rules.OvulationReturnDaysWithoutNursing * day);
        if (until < history.InfecundUntilTick) mother.SetReproduction(history with { InfecundUntilTick = until });
    }
}
